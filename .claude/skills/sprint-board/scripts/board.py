import datetime
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
BOARD_PATH = os.environ.get("BOARD_JSON") or os.path.join(ROOT, "docs", "trello_board.json")

USAGE = """usage: python .claude/skills/sprint-board/scripts/board.py <command>

  status                      progress per sprint
  next                        first open, unblocked card and its pending items
  sprint <n>                  cards of sprint n
  card <ID>                   full card with a numbered checklist
  check <ID> <term|#n>...     mark items as done
  uncheck <ID> <term|#n>...   mark items as pending
  close <ID>                  mark every item of the card as done
  block <ID> "<reason>"       flag the card as blocked
  unblock <ID>                clear the blocked flag
  blockers                    blocked cards and current-sprint cards waiting on dependencies
  add <ID> "<text>"           append a pending item to the card checklist
  edit <ID> <term|#n> "<text>"
                              rewrite an item, keeping its state
  depend <ID> <DEP>...        add dependencies, pulling a same-sprint dependency before the card
  new <ID> "<title>" <sprint> "<summary>" ["<deps>"] ["<estimate>"] ["<item|item>"] ["<label|label>"]
                              create a card at the end of its sprint
  verify                      ids, dependencies, cycles and blocker placement
"""

GREEN = "\033[32m"
GRAY = "\033[90m"
RED = "\033[31m"
YELLOW = "\033[33m"
BOLD = "\033[1m"
RESET = "\033[0m"

DONE_MARK = f"{GREEN}✓{RESET}"
OPEN_MARK = f"{GRAY}·{RESET}"
BLOCKED_MARK = f"{RED}x{RESET}"
WAITING_MARK = f"{YELLOW}▸{RESET}"

ID_PATTERN = re.compile(r"^\[([A-Z0-9]+-\d+)\]")
ID_REFERENCE_PATTERN = re.compile(r"\[([A-Z0-9]+-\d+)\]")
ITEM_PATTERN = re.compile(r"^- \[( |x|X)\] (.*)$")
OPEN_ITEM_PATTERN = re.compile(r"(?m)^- \[ \] ")
DEPENDS_PATTERN = re.compile(r"\*\*Depende de:\*\*([^\n]*)")
ESTIMATE_PATTERN = re.compile(r"\*\*Estimativa:\*\*\s*([^\n·]+)")
SPRINT_PATTERN = re.compile(r"^Sprint (\d+)")

BLOCKER_LABEL = "Bloqueante"
CRITERIA_HEADING = "**Critérios de aceitação:**"
METADATA_PREFIXES = ("**Depende de:**", "**Estimativa:**")
STALE_BLOCK_DAYS = 3
MAX_SPRINT_GAP = 1


def load():
    if not os.path.exists(BOARD_PATH):
        sys.exit(f"board not found at {BOARD_PATH}; set BOARD_JSON or place it in docs/")
    with open(BOARD_PATH, encoding="utf-8") as handle:
        return json.load(handle)


def save(board):
    with open(BOARD_PATH, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(board, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def card_id(card):
    match = ID_PATTERN.match(card["title"])
    return match.group(1) if match else None


def find(board, wanted):
    wanted = wanted.strip("[]").upper()
    for card in board["cards"]:
        if card_id(card) == wanted:
            return card
    sys.exit(f"card {wanted} not found; run 'status' or 'sprint <n>' to list ids")


def sprint_of(card):
    for label in card.get("labels", []):
        match = SPRINT_PATTERN.match(label)
        if match:
            return int(match.group(1))
    return None


def sprint_name(board, number):
    if number is None:
        return "unscheduled"
    for label in board.get("labels", []):
        match = SPRINT_PATTERN.match(label["name"])
        if match and int(match.group(1)) == number:
            return label["name"]
    return f"Sprint {number:02d}"


def items(card):
    for index, line in enumerate(card["description"].split("\n")):
        match = ITEM_PATTERN.match(line)
        if match:
            yield index, match.group(2), match.group(1) != " "


def progress(card):
    states = [done for _, _, done in items(card)]
    return sum(states), len(states)


def is_done(card):
    done, total = progress(card)
    return total > 0 and done == total


def dependencies(card):
    match = DEPENDS_PATTERN.search(card["description"])
    return ID_REFERENCE_PATTERN.findall(match.group(1)) if match else []


def estimate(card):
    match = ESTIMATE_PATTERN.search(card["description"])
    return match.group(1).strip() if match else None


def summary(card):
    first_paragraph = card["description"].split("\n\n", 1)[0].strip()
    return "" if first_paragraph.startswith(METADATA_PREFIXES) else first_paragraph


def done_ids(board):
    return {card_id(card) for card in board["cards"] if is_done(card)}


def is_unblocked(board, card):
    finished = done_ids(board)
    return all(dependency in finished for dependency in dependencies(card))


def scheduled(board):
    return sorted((card for card in board["cards"] if sprint_of(card) is not None), key=sprint_of)


def sprint_numbers(board):
    return sorted({sprint_of(card) for card in scheduled(board)})


def current_sprint(board):
    open_sprints = [sprint_of(card) for card in scheduled(board) if not is_done(card)]
    return min(open_sprints) if open_sprints else None


def bar(done, total, width=16):
    if total == 0:
        return " " * width
    filled = round(width * done / total)
    return "█" * filled + "░" * (width - filled)


def print_header(board, card):
    print(f"\n{BOLD}{card['title']}{RESET}   {GRAY}{sprint_name(board, sprint_of(card))}{RESET}")


def state_mark(board, card):
    if is_done(card):
        return DONE_MARK
    if card.get("blocked"):
        return BLOCKED_MARK
    return OPEN_MARK if is_unblocked(board, card) else WAITING_MARK


def command_status(board):
    print(f"\n{BOLD}{board['board_name']}{RESET}\n")
    grand_done = grand_total = 0
    for number in sprint_numbers(board):
        cards = [card for card in board["cards"] if sprint_of(card) == number]
        done = sum(progress(card)[0] for card in cards)
        total = sum(progress(card)[1] for card in cards)
        grand_done += done
        grand_total += total
        open_count = sum(1 for card in cards if not is_done(card))
        percent = f"{100 * done // total:3d}%" if total else "  —"
        mark = BLOCKED_MARK if any(card.get("blocked") for card in cards) else " "
        print(f"{mark} {bar(done, total)} {percent}  {sprint_name(board, number)}  {GRAY}({open_count}/{len(cards)} open){RESET}")
    unscheduled = [card_id(card) for card in board["cards"] if sprint_of(card) is None]
    if unscheduled:
        print(f"\n  {GRAY}unscheduled: {', '.join(unscheduled)}{RESET}")
    overall = 100 * grand_done // grand_total if grand_total else 0
    print(f"\n  total: {grand_done}/{grand_total} items ({overall}%)\n")


def command_next(board):
    for card in scheduled(board):
        if is_done(card) or card.get("blocked") or not is_unblocked(board, card):
            continue
        print_header(board, card)
        if summary(card):
            print(f"  {summary(card)}")
        if dependencies(card):
            print(f"  {GRAY}depends on: {', '.join(dependencies(card))} (all done){RESET}")
        if estimate(card):
            print(f"  {GRAY}estimate: {estimate(card)}{RESET}")
        print()
        for _, text, done in items(card):
            if not done:
                print(f"  {OPEN_MARK} {text}")
        print()
        return
    print("\nnothing unblocked; run 'blockers' or 'status'\n")


def command_sprint(board, number):
    number = int(number)
    cards = [card for card in board["cards"] if sprint_of(card) == number]
    if not cards:
        sys.exit(f"sprint {number} has no cards")
    print(f"\n{BOLD}{sprint_name(board, number)}{RESET}\n")
    for card in cards:
        done, total = progress(card)
        tag = f" {RED}[BLOCKER]{RESET}" if BLOCKER_LABEL in card.get("labels", []) else ""
        print(f"  {state_mark(board, card)} {card['title']}{tag}  {GRAY}{done}/{total}{RESET}")
        if card.get("blocked"):
            print(f"      {RED}{card['blocked']['reason']}{RESET}")
    print()


def command_card(board, wanted):
    card = find(board, wanted)
    done, total = progress(card)
    print_header(board, card)
    print(f"  {GRAY}{' '.join('#' + label for label in card.get('labels', []))}{RESET}")
    finished = done_ids(board)
    if dependencies(card):
        states = ", ".join(dependency if dependency in finished else f"{dependency} (pending)" for dependency in dependencies(card))
        print(f"\n  depends on: {states}")
    if card.get("blocked"):
        print(f"\n  {BLOCKED_MARK} blocked since {card['blocked']['since']}: {card['blocked']['reason']}")
    print(f"\n  progress: {bar(done, total)} {done}/{total}\n")
    number = 0
    for line in card["description"].split("\n"):
        match = ITEM_PATTERN.match(line)
        if match:
            number += 1
            mark = DONE_MARK if match.group(1) != " " else OPEN_MARK
            print(f"    {mark} {GRAY}#{number}{RESET} {match.group(2)}")
        else:
            print(f"  {line}")
    print()


def resolve(card, terms):
    entries = list(items(card))
    chosen = []
    for term in terms:
        if term.startswith("#") and term[1:].isdigit():
            position = int(term[1:])
            if not 1 <= position <= len(entries):
                sys.exit(f"{term} out of range (1..{len(entries)})")
            chosen.append(entries[position - 1])
            continue
        matches = [entry for entry in entries if term.lower() in entry[1].lower()]
        if not matches:
            sys.exit(f"no item matches {term!r}")
        if len(matches) > 1:
            print(f"{term!r} matches {len(matches)} items:")
            for _, text, _ in matches:
                print(f"   - {text}")
            sys.exit("use a more specific term or #n")
        chosen.append(matches[0])
    return chosen


def report(board, card, was_done):
    done, total = progress(card)
    if is_done(card) and not was_done:
        print(f"\n  {DONE_MARK} {BOLD}{card['title']} done{RESET}: all {total} items checked")
        identifier = card_id(card)
        unlocked = [
            other["title"]
            for other in board["cards"]
            if identifier in dependencies(other) and not is_done(other) and is_unblocked(board, other)
        ]
        if unlocked:
            print(f"  unlocked: {', '.join(unlocked)}")
        return
    print(f"\n  {done}/{total} {card['title']}")


def set_items(board, wanted, terms, done):
    if not terms:
        sys.exit(USAGE)
    card = find(board, wanted)
    was_done = is_done(card)
    lines = card["description"].split("\n")
    for index, text, _ in resolve(card, terms):
        lines[index] = f"- [{'x' if done else ' '}] {text}"
        print(f"  {DONE_MARK if done else OPEN_MARK} {text}")
    card["description"] = "\n".join(lines)
    if is_done(card):
        card.pop("blocked", None)
    report(board, card, was_done)
    save(board)


def command_check(board, wanted, *terms):
    set_items(board, wanted, terms, True)


def command_uncheck(board, wanted, *terms):
    set_items(board, wanted, terms, False)


def command_close(board, wanted):
    card = find(board, wanted)
    was_done = is_done(card)
    card["description"] = OPEN_ITEM_PATTERN.sub("- [x] ", card["description"])
    card.pop("blocked", None)
    report(board, card, was_done)
    save(board)


def command_block(board, wanted, reason):
    card = find(board, wanted)
    card["blocked"] = {"reason": reason, "since": datetime.date.today().isoformat()}
    print(f"  {BLOCKED_MARK} {card['title']}: {reason}")
    save(board)


def command_unblock(board, wanted):
    card = find(board, wanted)
    if card.pop("blocked", None) is None:
        sys.exit(f"{card['title']} is not blocked")
    print(f"  {DONE_MARK} {card['title']} unblocked")
    save(board)


def command_blockers(board):
    today = datetime.date.today()
    found = False
    for card in board["cards"]:
        blocked = card.get("blocked")
        if not blocked:
            continue
        found = True
        days = (today - datetime.date.fromisoformat(blocked["since"])).days
        age = f"{RED}({days}d){RESET}" if days >= STALE_BLOCK_DAYS else f"({days}d)"
        print(f"  {BLOCKED_MARK} {card['title']} {age}\n      {blocked['reason']}")
    finished = done_ids(board)
    sprint = current_sprint(board)
    for card in scheduled(board):
        if sprint_of(card) != sprint or is_done(card) or card.get("blocked"):
            continue
        missing = [dependency for dependency in dependencies(card) if dependency not in finished]
        if missing:
            found = True
            print(f"  {WAITING_MARK} {card['title']} {GRAY}waiting on {', '.join(missing)}{RESET}")
    if not found:
        print("  nothing blocked")


def command_add(board, wanted, text):
    card = find(board, wanted)
    lines = card["description"].split("\n")
    positions = [index for index, _, _ in items(card)]
    new_item = f"- [ ] {text}"
    if positions:
        lines.insert(positions[-1] + 1, new_item)
    else:
        lines.extend(["", CRITERIA_HEADING, new_item])
    card["description"] = "\n".join(lines)
    print(f"  + {card['title']}: {text}")
    save(board)


def command_edit(board, wanted, term, text):
    card = find(board, wanted)
    (index, previous, done), = resolve(card, [term])
    lines = card["description"].split("\n")
    lines[index] = f"- [{'x' if done else ' '}] {text}"
    card["description"] = "\n".join(lines)
    print(f"  {DONE_MARK if done else OPEN_MARK} {card['title']}\n      - {previous}\n      + {text}")
    save(board)


def with_dependency(description, reference):
    listed = re.search(r"\*\*Depende de:\*\*[^\n·]*[^\s·]", description)
    if listed:
        return f"{description[:listed.end()]}, {reference}{description[listed.end():]}"
    if "**Estimativa:**" in description:
        return description.replace("**Estimativa:**", f"**Depende de:** {reference} · **Estimativa:**", 1)
    return description.replace(CRITERIA_HEADING, f"**Depende de:** {reference}\n\n{CRITERIA_HEADING}", 1)


def pull_before(board, dependency, consumer):
    cards = board["cards"]
    if sprint_of(dependency) != sprint_of(consumer) or cards.index(dependency) < cards.index(consumer):
        return False
    cards.remove(dependency)
    cards.insert(cards.index(consumer), dependency)
    return True


def command_depend(board, wanted, *dependencies_wanted):
    if not dependencies_wanted:
        sys.exit(USAGE)
    card = find(board, wanted)
    for dependency_wanted in dependencies_wanted:
        dependency = find(board, dependency_wanted)
        identifier = card_id(dependency)
        if identifier == card_id(card):
            sys.exit(f"{identifier} cannot depend on itself")
        if identifier in dependencies(card):
            print(f"  {GRAY}{card['title']} already depends on {identifier}{RESET}")
            continue
        card["description"] = with_dependency(card["description"], f"[{identifier}]")
        moved = pull_before(board, dependency, card)
        note = f"  {GRAY}(moved before {card_id(card)}){RESET}" if moved else ""
        print(f"  + {card['title']} depends on {identifier}{note}")
    save(board)


def sprint_label(board, number):
    for label in board.get("labels", []):
        match = SPRINT_PATTERN.match(label["name"])
        if match and int(match.group(1)) == number:
            return label["name"]
    sys.exit(f"no label for sprint {number}; add it to the board labels first")


def insert_position(board, number):
    last = None
    for position, card in enumerate(board["cards"]):
        if sprint_of(card) == number:
            last = position
    return len(board["cards"]) if last is None else last + 1


def command_new(board, wanted, title, sprint, summary, depends="", estimate="", checklist="", labels=""):
    identifier = wanted.strip("[]").upper()
    if any(card_id(card) == identifier for card in board["cards"]):
        sys.exit(f"card {identifier} already exists")
    number = int(sprint)
    texts = [text.strip() for text in checklist.split("|") if text.strip()]
    if not texts:
        sys.exit("a new card needs at least one checklist item")

    metadata = []
    if depends.strip():
        metadata.append(f"**Depende de:** {depends.strip()}")
    if estimate.strip():
        metadata.append(f"**Estimativa:** {estimate.strip()}")

    body = []
    if summary.strip():
        body.append(summary.strip())
    if metadata:
        body.append(" · ".join(metadata))
    body.append("\n".join([CRITERIA_HEADING] + [f"- [ ] {text}" for text in texts]))

    card = {
        "title": f"[{identifier}] {title}",
        "labels": [sprint_label(board, number)] + [label.strip() for label in labels.split("|") if label.strip()],
        "description": "\n\n".join(body),
    }
    board["cards"].insert(insert_position(board, number), card)
    print(f"  + {card['title']}  {GRAY}{sprint_label(board, number)} · {len(texts)} item(s){RESET}")
    save(board)


def find_cycles(graph):
    state = {}
    cycles = []

    def visit(node, path):
        if state.get(node) == "active":
            cycles.append(path[path.index(node):] + [node])
            return
        if state.get(node) == "done" or node not in graph:
            return
        state[node] = "active"
        for dependency in graph[node]:
            visit(dependency, path + [node])
        state[node] = "done"

    for node in graph:
        visit(node, [])
    return cycles


def command_verify(board):
    issues = []
    seen = set()
    for card in board["cards"]:
        identifier = card_id(card)
        if identifier is None:
            issues.append(f"card without [ID] in title: {card['title']}")
        elif identifier in seen:
            issues.append(f"duplicated id: {identifier}")
        seen.add(identifier)

    by_id = {card_id(card): (position, card) for position, card in enumerate(board["cards"]) if card_id(card)}
    for position, card in enumerate(board["cards"]):
        for dependency in dependencies(card):
            if dependency not in by_id:
                issues.append(f"{card_id(card)} depends on {dependency}, which does not exist")
                continue
            dependency_position, dependency_card = by_id[dependency]
            dependency_sprint, consumer_sprint = sprint_of(dependency_card), sprint_of(card)
            if dependency_sprint is None or consumer_sprint is None:
                continue
            if dependency_sprint > consumer_sprint:
                issues.append(f"{card_id(card)} (sprint {consumer_sprint}) depends on {dependency} scheduled later (sprint {dependency_sprint})")
            elif dependency_sprint == consumer_sprint and dependency_position > position:
                issues.append(f"{card_id(card)} is listed before its same-sprint dependency {dependency}")

    graph = {identifier: dependencies(card) for identifier, (_, card) in by_id.items()}
    for cycle in find_cycles(graph):
        issues.append(f"cycle: {' -> '.join(cycle)}")

    for identifier, (_, card) in by_id.items():
        if BLOCKER_LABEL not in card.get("labels", []) or sprint_of(card) is None:
            continue
        consumer_sprints = [sprint_of(other) for _, other in by_id.values() if identifier in dependencies(other) and sprint_of(other) is not None]
        if not consumer_sprints:
            issues.append(f"{identifier} is a blocker but no card depends on it")
            continue
        gap = min(consumer_sprints) - sprint_of(card)
        if gap > MAX_SPRINT_GAP:
            issues.append(f"{identifier} is a blocker whose first consumer is {gap} sprints ahead; pull the consumer in or record the mitigation on both cards")

    for issue in issues:
        print(f"  {WAITING_MARK} {issue}")
    print(f"  verification finished: {len(issues)} issue(s)")


COMMANDS = {
    "status": (command_status, 0),
    "next": (command_next, 0),
    "sprint": (command_sprint, 1),
    "card": (command_card, 1),
    "check": (command_check, 2),
    "uncheck": (command_uncheck, 2),
    "close": (command_close, 1),
    "block": (command_block, 2),
    "unblock": (command_unblock, 1),
    "blockers": (command_blockers, 0),
    "add": (command_add, 2),
    "edit": (command_edit, 3),
    "depend": (command_depend, 2),
    "new": (command_new, 4),
    "verify": (command_verify, 0),
}


def main():
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2 or sys.argv[1] not in COMMANDS:
        print(USAGE)
        return
    handler, required = COMMANDS[sys.argv[1]]
    arguments = sys.argv[2:]
    if len(arguments) < required:
        sys.exit(USAGE)
    handler(load(), *arguments)


if __name__ == "__main__":
    main()
