from __future__ import annotations

import argparse
import json
from collections import deque
from pathlib import Path

SIZE = 48
MAP_PATH = Path("Assets/_Game/Data/Maps/kerameikos.json")
AGORA_PATH = Path("Assets/_Game/Data/Maps/agora.json")

DIRT, PACKED, CLAY, STRAW, GRASS, STONE, WATER = "d", "p", "c", "s", "g", "t", "w"
NOTHING = "-"
BRICK, MARBLE, KILN, WHEEL, AMPHORA, TRUNK, COLUMN, MARKER, STALL = "B", "W", "K", "P", "A", "T", "C", "H", "M"

FLOOR_LEGEND = {
    DIRT: "floor_dirt",
    PACKED: "floor_packed_dirt",
    CLAY: "floor_dry_clay",
    STRAW: "floor_straw",
    GRASS: "floor_grass",
    STONE: "floor_stone",
    WATER: "floor_water",
}

BLOCK_LEGEND = {
    BRICK: "wall_brick",
    MARBLE: "wall_marble",
    KILN: "block_kiln",
    WHEEL: "prop_potters_wheel",
    AMPHORA: "prop_amphora_stack",
    TRUNK: "prop_olive_trunk",
    COLUMN: "block_doric_column",
    MARKER: "prop_statue_base",
    STALL: "prop_market_stall",
}

COMPOUND_FLOOR = [
    "dddddddddddddddd",
    "dddddddddddddddd",
    "ccccdccccccccccc",
    "ccccdccccccccccc",
    "ccccdccccccccccc",
    "ccccdccccccccccc",
    "cccddddccccccccc",
    "pppppppppccccccc",
    "pppppppppccccccc",
    "pppppppppccccccc",
    "pppppppppccccccc",
    "pppppppppcccssss",
    "pppppppppcccssss",
    "pppppppppcccssss",
    "pppppppppcccssss",
    "pppppppppcccssss",
]

COMPOUND_BLOCKS = [
    "----------------",
    "----------------",
    "----------------",
    "-T--------------",
    "----------------",
    "----------------",
    "-----AAA--------",
    "BBBB-BBBB-------",
    "B-------B-------",
    "B-------B--K----",
    "B-------B-A-----",
    "B--P----B-------",
    "B-------B----T--",
    "B-------B-------",
    "BAA---AAB-----T-",
    "BBBBBBBBB-------",
]

COMPOUND_LEVELS = ["0" * 16, "0" * 16] + ["1" * 16] * 14

STREETS = [
    (0, 0, 17, 1, DIRT, 0),
    (16, 2, 17, 21, DIRT, 0),
    (16, 10, 47, 11, DIRT, 0),
    (44, 12, 45, 19, DIRT, 0),
    (16, 20, 47, 21, DIRT, 0),
    (30, 22, 47, 23, DIRT, 0),
    (28, 22, 29, 27, DIRT, 0),
    (28, 28, 29, 47, DIRT, 1),
    (0, 28, 27, 29, DIRT, 0),
    (0, 30, 1, 39, DIRT, 0),
    (0, 40, 1, 47, DIRT, 1),
    (2, 40, 27, 41, DIRT, 1),
    (30, 34, 47, 35, DIRT, 1),
]

PLOTS = [
    (20, 4, 25, 8, CLAY, 1),
    (22, 9, 23, 9, CLAY, 1),
    (32, 4, 39, 8, STRAW, 1),
    (35, 9, 36, 9, STRAW, 1),
    (42, 4, 46, 8, CLAY, 1),
    (44, 9, 45, 9, CLAY, 1),
    (36, 14, 43, 19, CLAY, 1),
    (0, 18, 15, 27, GRASS, 0),
    (32, 24, 39, 33, GRASS, 1),
    (42, 26, 46, 33, CLAY, 1),
    (34, 36, 43, 44, GRASS, 1),
    (2, 30, 27, 39, STONE, 2),
    (8, 30, 13, 31, STONE, 1),
    (2, 42, 27, 47, GRASS, 2),
    (8, 43, 19, 46, GRASS, 3),
]

RIVER_CREST = [25, 25, 25, 24, 24, 23, 23, 23, 24, 24, 25, 26, 26, 25, 25, 24]

TALL_HOUSES = [
    (18, 12, 27, 19),
    (30, 36, 47, 47),
    (2, 16, 15, 17),
    (42, 12, 43, 19),
    (18, 2, 29, 3),
    (30, 0, 35, 9),
    (44, 0, 47, 9),
    (30, 24, 31, 33),
    (16, 22, 27, 27),
]

TOWERS = [
    (24, 12, 27, 15),
    (44, 4, 47, 7),
    (30, 44, 33, 47),
    (0, 16, 3, 17),
]

ENCLOSURES = [
    (3, 32, 6, 34),
    (9, 33, 12, 34),
    (15, 32, 17, 35),
    (20, 33, 24, 34),
    (3, 37, 5, 39),
    (8, 37, 12, 38),
    (15, 38, 18, 39),
    (21, 37, 24, 39),
    (26, 31, 27, 36),
]

GRAVE_MARKERS = [(7, 33), (13, 33), (19, 33), (25, 33), (7, 38), (13, 38), (19, 38), (25, 38),
                 (2, 31), (8, 31), (14, 31), (20, 31), (4, 36), (10, 36), (16, 36), (22, 36)]
GRAVE_COLUMNS = [(5, 36), (11, 36), (17, 36), (23, 36), (4, 30), (17, 30), (25, 30)]

ORCHARDS = [(2, 42, 27, 47, 3, 2), (32, 24, 39, 33, 3, 3), (34, 36, 43, 44, 3, 3)]

KILNS = [(21, 6), (24, 4), (33, 6), (38, 7), (43, 5), (43, 28), (45, 31)]
AMPHORAE = [(20, 8), (25, 7), (22, 4), (32, 8), (35, 4), (39, 4), (42, 8), (46, 5),
            (38, 15), (40, 17), (42, 15), (44, 27), (46, 30), (42, 32), (20, 6), (37, 6)]
STALLS = [(2, 0), (7, 0), (12, 0),
          (19, 10), (29, 10), (39, 10),
          (19, 21), (33, 21), (43, 21), (37, 23),
          (17, 8), (17, 14), (17, 19),
          (28, 25), (28, 37), (5, 28), (21, 28),
          (11, 41), (25, 41), (33, 35), (45, 35)]
DOORWAYS = [(25, 10), (33, 10), (42, 10), (46, 10),
            (23, 21), (38, 21), (46, 21), (32, 23), (46, 23),
            (28, 31), (28, 43), (13, 28),
            (4, 41), (18, 41), (0, 33), (0, 38), (0, 44),
            (44, 14), (44, 18), (39, 35)]

EXITS = [(16, 0), (17, 0)]
ARRIVAL = (16, 2)
KLEIA = (4, 11)
ARISTEU = (6, 13)
NEIGHBOUR = (10, 1)
MENEXENO = (3, 6)
DAMARIS = (16, 4)
CUSTOMERS = [(5, 5), (6, 5)]
DOOR = (4, 7)
WHEEL_CORNER = (4, 8)
GLANCE = (3, 0)
DEPARTURE = (17, 0)

DUEL_WON = [{"flag": "duel_thrasybios", "equals": "won"}, {"flag": "maieutics_unlocked", "absent": True}]
OFFER_OPEN = [{"flag": "pressure_tools"}, {"flag": "maieutics_unlocked"}, {"flag": "damaris_branch", "equals": "undecided"}]


def blank_grid(value):
    return [[value] * SIZE for _ in range(SIZE)]


def paint(grid, rect, value):
    x0, y0, x1, y1 = rect[:4]
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            grid[y][x] = value


def build_terrain():
    floor = blank_grid(PACKED)
    blocks = blank_grid(BRICK)
    levels = blank_grid(1)

    for rect in TALL_HOUSES:
        paint(levels, rect, 2)

    for rect in TOWERS:
        paint(levels, rect, 3)

    for area in PLOTS + STREETS:
        x0, y0, x1, y1, tile, level = area
        paint(floor, (x0, y0, x1, y1), tile)
        paint(blocks, (x0, y0, x1, y1), NOTHING)
        paint(levels, (x0, y0, x1, y1), level)

    for x, crest in enumerate(RIVER_CREST):
        for y in range(18, crest + 1):
            floor[y][x] = WATER

    for y, row in enumerate(COMPOUND_FLOOR):
        for x, tile in enumerate(row):
            floor[y][x] = tile
            blocks[y][x] = COMPOUND_BLOCKS[y][x]
            levels[y][x] = int(COMPOUND_LEVELS[y][x])

    for rect in ENCLOSURES:
        paint(blocks, rect, MARBLE)

    for x0, y0, x1, y1, step_x, step_y in ORCHARDS:
        for y in range(y0, y1 + 1, step_y):
            for x in range(x0 + (y * 5 // step_y) % step_x, x1 + 1, step_x):
                if (x * 7 + y * 11) % 7 == 0:
                    continue
                blocks[y][x] = TRUNK

    for x, y in GRAVE_MARKERS:
        blocks[y][x] = MARKER
    for x, y in GRAVE_COLUMNS:
        blocks[y][x] = COLUMN
    for x, y in KILNS:
        blocks[y][x] = KILN
    for x, y in AMPHORAE + DOORWAYS:
        blocks[y][x] = AMPHORA
    for x, y in STALLS:
        blocks[y][x] = STALL

    return floor, blocks, levels


def walkable(floor, blocks, x, y):
    return floor[y][x] != WATER and blocks[y][x] == NOTHING


def neighbours(x, y):
    return [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]


def inside(cell):
    return 0 <= cell[0] < SIZE and 0 <= cell[1] < SIZE


def can_step(floor, blocks, levels, a, b):
    if not inside(b):
        return False
    if not walkable(floor, blocks, *a) or not walkable(floor, blocks, *b):
        return False
    return abs(levels[a[1]][a[0]] - levels[b[1]][b[0]]) <= 1


def flood(floor, blocks, levels, start, allowed=None):
    seen = {start}
    queue = deque([start])
    while queue:
        cell = queue.popleft()
        for step in neighbours(*cell):
            if step in seen or not can_step(floor, blocks, levels, cell, step):
                continue
            if allowed is not None and not allowed(step):
                continue
            seen.add(step)
            queue.append(step)
    return seen


def distances(floor, blocks, levels, start):
    reach = {start: 0}
    queue = deque([start])
    while queue:
        cell = queue.popleft()
        for step in neighbours(*cell):
            if step in reach or not can_step(floor, blocks, levels, cell, step):
                continue
            reach[step] = reach[cell] + 1
            queue.append(step)
    return reach


def free_beside(floor, blocks, cell):
    return [step for step in neighbours(*cell) if inside(step) and walkable(floor, blocks, *step)]


def scene_triggers(cells, cutscene, flag, conditions):
    return [{"x": x, "y": y, "cutscene": cutscene, "flag": flag, "conditions": conditions} for x, y in cells]


def build_map():
    floor, blocks, levels = build_terrain()
    menexeno_cells = free_beside(floor, blocks, MENEXENO)
    damaris_cells = free_beside(floor, blocks, DAMARIS)

    actors = [
        {"character": "kleia", "x": KLEIA[0], "y": KLEIA[1], "facing": "SouthWest"},
        {"character": "aristeu", "x": ARISTEU[0], "y": ARISTEU[1], "facing": "SouthWest", "dialogue": "aristeu_workshop"},
        {"character": "citizen", "x": NEIGHBOUR[0], "y": NEIGHBOUR[1], "facing": "SouthWest", "dialogue": "neighbor_kerameikos"},
        {"character": "menexeno", "x": MENEXENO[0], "y": MENEXENO[1], "facing": "NorthEast",
         "enters": {"x": EXITS[1][0], "y": EXITS[1][1]}, "conditions": DUEL_WON},
        {"character": "citizen", "x": CUSTOMERS[0][0], "y": CUSTOMERS[0][1], "facing": "NorthWest",
         "dialogue": "customer_oil", "conditions": [{"flag": "pressure_deserted", "absent": True}]},
        {"character": "citizen", "x": CUSTOMERS[1][0], "y": CUSTOMERS[1][1], "facing": "NorthWest",
         "dialogue": "customer_crater", "conditions": [{"flag": "pressure_deserted", "absent": True}]},
        {"character": "damaris", "x": DAMARIS[0], "y": DAMARIS[1], "facing": "NorthEast",
         "enters": {"x": EXITS[0][0], "y": EXITS[0][1]}, "conditions": OFFER_OPEN},
    ]

    triggers = [{"x": DOOR[0], "y": DOOR[1], "dialogue": "kerameikos_leaving_workshop", "flag": "left_workshop"}]
    triggers += scene_triggers(menexeno_cells, "menexeno_follows_home", "menexeno_door_scene", DUEL_WON)
    triggers += scene_triggers(damaris_cells, "damaris_seeks_kleia", "damaris_offer_scene", OFFER_OPEN)
    triggers.append({"x": WHEEL_CORNER[0], "y": WHEEL_CORNER[1], "dialogue": "kerameikos_wheel_sold",
                     "flag": "saw_wheel_sold", "conditions": [{"flag": "pressure_tools"}]})

    return {
        "id": "kerameikos",
        "home": True,
        "fogRevealDepth": 0,
        "fogReveal": [{"x": 0, "y": 7, "width": 9, "height": 9}],
        "floorLegend": FLOOR_LEGEND,
        "blockLegend": BLOCK_LEGEND,
        "floor": ["".join(row) for row in floor],
        "blocks": ["".join(row) for row in blocks],
        "heights": ["".join(str(level) for level in row) for row in levels],
        "actors": actors,
        "conditionalBlocks": [
            {"x": 3, "y": 11, "conditions": [{"flag": "pressure_tools", "absent": True}]},
            {"x": 7, "y": 6, "conditions": [{"flag": "pressure_deserted"}]},
        ],
        "exits": [{"x": x, "y": y, "location": "agora", "arrivalX": 0, "arrivalY": 1,
                   "facing": "NorthEast", "label": "Para a Ágora"} for x, y in EXITS],
        "triggers": triggers,
        "cutscenes": [
            {
                "id": "menexeno_follows_home",
                "steps": [
                    {"at": 0.25, "action": "Face", "actor": "kleia", "toward": "menexeno"},
                    {"at": 0.5, "action": "Face", "actor": "menexeno", "toward": "kleia"},
                    {"at": 0.75, "action": "Dialogue", "dialogue": "menexeno_door_offer"},
                    {"at": 1.5, "action": "Face", "actor": "menexeno", "x": GLANCE[0], "y": GLANCE[1]},
                    {"at": 2.25, "action": "Dialogue", "dialogue": "menexeno_door_glance"},
                    {"at": 2.75, "action": "Face", "actor": "menexeno", "toward": "kleia"},
                    {"at": 3.0, "action": "Dialogue", "dialogue": "menexeno_door_lesson"},
                    {"at": 3.5, "action": "Walk", "actor": "menexeno", "x": DEPARTURE[0], "y": DEPARTURE[1]},
                    {"at": 3.75, "action": "Leave", "actor": "menexeno"},
                    {"at": 4.5, "action": "Dialogue", "dialogue": "menexeno_door_learned"},
                ],
            },
            {
                "id": "damaris_seeks_kleia",
                "steps": [
                    {"at": 0.25, "action": "Face", "actor": "kleia", "toward": "damaris"},
                    {"at": 0.5, "action": "Face", "actor": "damaris", "toward": "kleia"},
                    {"at": 0.75, "action": "Dialogue", "dialogue": "damaris_offer"},
                    {"at": 1.5, "action": "Walk", "actor": "damaris", "x": DEPARTURE[0], "y": DEPARTURE[1]},
                    {"at": 1.75, "action": "Leave", "actor": "damaris"},
                    {"at": 2.5, "action": "Dialogue", "dialogue": "damaris_technique"},
                ],
            },
        ],
        "cues": [
            {"on": "MapStart", "dialogue": "kerameikos_first_steps", "flag": "tutorial_first_steps",
             "conditions": [{"flag": "left_workshop", "absent": True}]},
            {"on": "FogRevealed", "dialogue": "kerameikos_tutorial_fog", "flag": "tutorial_fog",
             "conditions": [{"flag": "tutorial_first_steps"}]},
            {"on": "WalkFinished", "dialogue": "kerameikos_tutorial_talk", "flag": "tutorial_talk",
             "conditions": [{"flag": "tutorial_fog"}, {"flag": "aristeu_errand", "absent": True}]},
            {"on": "Idle", "dialogue": "kerameikos_no_customers", "flag": "saw_empty_workshop",
             "conditions": [{"flag": "pressure_deserted"}]},
        ],
    }


def complain(problems, condition, message):
    if not condition:
        problems.append(message)


def verify(layout):
    problems = []
    floor = [list(row) for row in layout["floor"]]
    blocks = [list(row) for row in layout["blocks"]]
    levels = [[int(char) for char in row] for row in layout["heights"]]

    complain(problems, all(len(row) == SIZE for row in floor) and len(floor) == SIZE, "the floor is not square")
    complain(problems, all(len(row) == SIZE for row in blocks) and len(blocks) == SIZE, "the blocks do not match the floor")
    complain(problems, all(len(row) == SIZE for row in levels) and len(levels) == SIZE, "the heights do not match the floor")
    complain(problems, all(0 <= level <= 3 for row in levels for level in row), "a height leaves the range 0 to 3")

    cells = [(x, y) for y in range(SIZE) for x in range(SIZE)]
    open_cells = [cell for cell in cells if walkable(floor, blocks, *cell)]
    reached = flood(floor, blocks, levels, KLEIA)
    sealed = [cell for cell in open_cells if cell not in reached]
    complain(problems, not sealed, f"{len(sealed)} walkable cells are sealed off, such as {sealed[:6]}")

    actors = layout["actors"]
    standing = {(actor["x"], actor["y"]) for actor in actors[1:]}
    for actor in actors:
        cell = (actor["x"], actor["y"])
        complain(problems, walkable(floor, blocks, *cell), f"{actor['character']} stands on a blocked cell {cell}")
        complain(problems, free_beside(floor, blocks, cell), f"nobody can stand beside {actor['character']}")
        if "enters" in actor:
            door = (actor["enters"]["x"], actor["enters"]["y"])
            complain(problems, door in flood(floor, blocks, levels, cell), f"{actor['character']} cannot walk in from {door}")

    without_actors = flood(floor, blocks, levels, KLEIA, lambda cell: cell not in standing)
    hidden = [cell for cell in open_cells
              if cell not in standing and cell not in without_actors
              and not any(abs(cell[0] - actor[0]) + abs(cell[1] - actor[1]) == 1 for actor in standing)]
    complain(problems, not hidden, f"an actor blocks the only way to {hidden[:6]}")

    for exit_link in layout["exits"]:
        cell = (exit_link["x"], exit_link["y"])
        complain(problems, floor[cell[1]][cell[0]] == DIRT, f"the exit at {cell} does not leave from the street")
        complain(problems, cell[0] in (0, SIZE - 1) or cell[1] in (0, SIZE - 1), f"the exit at {cell} is not on the map edge")
        complain(problems, cell in reached, f"the exit at {cell} cannot be reached")

    door_scene = {(trigger["x"], trigger["y"]) for trigger in layout["triggers"] if trigger.get("cutscene") == "menexeno_follows_home"}
    behind_door = flood(floor, blocks, levels, KLEIA, lambda cell: cell not in door_scene)
    complain(problems, not any((link["x"], link["y"]) in behind_door for link in layout["exits"]),
             "Kleia can reach the road without passing Menexeno at the door")

    corner = {WHEEL_CORNER}
    behind_corner = flood(floor, blocks, levels, KLEIA, lambda cell: cell not in corner)
    complain(problems, not any((link["x"], link["y"]) in behind_corner for link in layout["exits"]),
             "Kleia can leave the workshop without crossing the wheel corner")

    complain(problems, floor[DAMARIS[1]][DAMARIS[0]] == DIRT, "Damaris does not wait in the street")
    complain(problems, abs(DAMARIS[0] - ARRIVAL[0]) + abs(DAMARIS[1] - ARRIVAL[1]) <= 2,
             "Damaris is more than two steps from where Kleia comes home")
    complain(problems, ARRIVAL not in free_beside(floor, blocks, DAMARIS), "the offer starts on the arrival cell")
    complain(problems, walkable(floor, blocks, *ARRIVAL) and ARRIVAL not in standing and ARRIVAL not in set(EXITS),
             "the arrival from the agora is not a free cell")

    trigger_cells = [(trigger["x"], trigger["y"]) for trigger in layout["triggers"]]
    complain(problems, len(trigger_cells) == len(set(trigger_cells)), "two triggers share a cell")
    for cell in trigger_cells:
        complain(problems, walkable(floor, blocks, *cell), f"the trigger at {cell} is not on a walkable cell")
        complain(problems, cell not in standing and cell not in set(EXITS), f"the trigger at {cell} shares its cell")

    for block in layout["conditionalBlocks"]:
        complain(problems, blocks[block["y"]][block["x"]] != NOTHING,
                 f"the conditional block at ({block['x']}, {block['y']}) has no block to hide")

    for cutscene in layout["cutscenes"]:
        for step in cutscene["steps"]:
            if step["action"] in ("Walk", "Face") and "x" in step:
                cell = (step["x"], step["y"])
                complain(problems, walkable(floor, blocks, *cell), f"{cutscene['id']} points at the blocked cell {cell}")

    complain(problems, DEPARTURE in set(EXITS), "the cutscenes do not send anyone to the road")
    complain(problems, floor[GLANCE[1]][GLANCE[0]] == DIRT, "Menexeno does not glance down the street")

    house = [(x, y) for y in range(7, 16) for x in range(0, 9)]
    complain(problems, all(cell in reached or not walkable(floor, blocks, *cell) for cell in house),
             "part of the revealed house cannot be walked to")

    return problems, open_cells, reached


def report(layout, open_cells, reached):
    floor = [list(row) for row in layout["floor"]]
    blocks = [list(row) for row in layout["blocks"]]
    levels = [[int(char) for char in row] for row in layout["heights"]]
    from_door = distances(floor, blocks, levels, DOOR)
    lines = [
        f"size            {SIZE}x{SIZE} = {SIZE * SIZE} cells",
        f"walkable        {len(open_cells)} ({100 * len(open_cells) // (SIZE * SIZE)}%)",
        f"reachable       {len(reached)}",
        f"door to the road {min(from_door[cell] for cell in EXITS)} steps",
        f"deepest cell    {max(from_door.values())} steps from the door",
        f"levels used     {sorted({level for row in levels for level in row})}",
    ]
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--print", action="store_true")
    arguments = parser.parse_args()

    layout = build_map()
    problems, open_cells, reached = verify(layout)
    if problems:
        for problem in problems:
            print(f"error: {problem}")
        raise SystemExit(1)

    print(report(layout, open_cells, reached))
    if arguments.print:
        for row in reversed(layout["blocks"]):
            print(row)
        return

    MAP_PATH.write_text(json.dumps(layout, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    agora = json.loads(AGORA_PATH.read_text(encoding="utf-8"))
    for link in agora["exits"]:
        if link["location"] == "kerameikos":
            link["arrivalX"], link["arrivalY"] = ARRIVAL
    AGORA_PATH.write_text(json.dumps(agora, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {MAP_PATH} and the arrival in {AGORA_PATH}")


if __name__ == "__main__":
    main()
