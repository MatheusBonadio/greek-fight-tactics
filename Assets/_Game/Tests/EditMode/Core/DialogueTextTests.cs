using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class DialogueTextTests
    {
        private const string Speech = "Kleia é filha de oleiro e mal sabe ler. Que valor pode ter o que ela diz sobre justiça? Ignorem a menina e escutem quem estudou.";

        [Test]
        public void Wrap_KeepsEveryLineWithinTheWidthWithoutLosingWords()
        {
            var lines = DialogueText.Wrap(Speech, 20);

            Assert.IsTrue(lines.All(line => line.Length <= 20));
            Assert.AreEqual(Speech.Split(' ').Length, lines.SelectMany(line => line.Split(' ')).Count());
            Assert.AreEqual(Speech, string.Join(" ", lines));
        }

        [Test]
        public void Wrap_SplitsWordsLongerThanTheLine()
        {
            var lines = DialogueText.Wrap("Anticonstitucionalissimamente curto", 10);

            CollectionAssert.AreEqual(new[] { "Anticonsti", "tucionalis", "simamente", "curto" }, lines);
        }

        [Test]
        public void Paginate_GroupsLinesIntoDialogueBoxPages()
        {
            var pages = DialogueText.Paginate(Speech, 20, 3);

            Assert.IsTrue(pages.Take(pages.Count - 1).All(page => page.Count == 3));
            Assert.AreEqual(DialogueText.Wrap(Speech, 20).Count, pages.Sum(page => page.Count));
        }

        [Test]
        public void EmptyText_HasNoLines()
        {
            Assert.IsEmpty(DialogueText.Wrap("   "));
            Assert.IsEmpty(DialogueText.Paginate(null));
        }
    }
}
