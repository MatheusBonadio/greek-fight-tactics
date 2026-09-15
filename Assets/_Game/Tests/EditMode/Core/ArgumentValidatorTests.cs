using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class ArgumentValidatorTests
    {
        private const string KnownFallacy = "ad_hominem";
        private const string ValidSpeech = "Quem nasceu no Cerâmico não tem autoridade para falar de justiça na ágora.";

        private static readonly string[] ValidPremises = { "Kleia nasceu no Cerâmico", "Oleiros não entendem de justiça" };

        private ArgumentValidator validator;

        [SetUp]
        public void SetUp()
        {
            validator = new ArgumentValidator(new[] { KnownFallacy, "straw_man" });
        }

        [Test]
        public void Validate_AcceptsAWellFormedArgument()
        {
            Assert.AreEqual(ArgumentViolation.None, validator.Validate(Build()));
        }

        [Test]
        public void Validate_AcceptsAParadoxWithoutFallacy()
        {
            var paradox = Build(fallacyId: null, tone: ArgumentTones.Calm);

            Assert.IsFalse(paradox.IsRefutable);
            Assert.AreEqual(ArgumentViolation.None, validator.Validate(paradox));
        }

        [Test]
        public void Validate_RejectsUnknownFallacy()
        {
            Assert.AreEqual(ArgumentViolation.UnknownFallacy, validator.Validate(Build(fallacyId: "zeno_tortoise")));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void Validate_RejectsWeakPremiseOutsideThePremises(int weakPremise)
        {
            Assert.AreEqual(ArgumentViolation.WeakPremiseOutOfRange, validator.Validate(Build(weakPremise: weakPremise)));
        }

        [Test]
        public void Validate_EnforcesSpeechLengthBounds()
        {
            var shortest = new string('a', ArgumentValidator.MinSpeechLength);
            var longest = new string('a', ArgumentValidator.MaxSpeechLength);

            Assert.AreEqual(ArgumentViolation.None, validator.Validate(Build(speech: shortest)));
            Assert.AreEqual(ArgumentViolation.None, validator.Validate(Build(speech: longest)));
            Assert.AreEqual(ArgumentViolation.SpeechTooShort, validator.Validate(Build(speech: shortest.Substring(1))));
            Assert.AreEqual(ArgumentViolation.SpeechTooLong, validator.Validate(Build(speech: longest + "a")));
            Assert.AreEqual(ArgumentViolation.MissingSpeech, validator.Validate(Build(speech: "   ")));
        }

        [Test]
        public void Validate_RejectsMissingOrBlankPremises()
        {
            Assert.AreEqual(ArgumentViolation.MissingPremises, validator.Validate(Build(premises: new string[0])));
            Assert.AreEqual(ArgumentViolation.EmptyPremise, validator.Validate(Build(premises: new[] { "válida", " " })));
        }

        [Test]
        public void Validate_RejectsUnknownTone()
        {
            Assert.AreEqual(ArgumentViolation.UnknownTone, validator.Validate(Build(tone: "furious")));
        }

        [Test]
        public void Validate_RejectsDefaultArgument()
        {
            Assert.AreEqual(ArgumentViolation.MissingSpeech, validator.Validate(default));
        }

        [Test]
        public void Argument_CopiesPremisesSoCallersCannotMutateIt()
        {
            var premises = new[] { "primeira", "segunda" };
            var argument = Build(premises: premises);

            premises[0] = "alterada";

            Assert.AreEqual("primeira", argument.Premises[0]);
        }

        private static Argument Build(
            string speech = ValidSpeech,
            string fallacyId = KnownFallacy,
            string[] premises = null,
            int weakPremise = 1,
            string tone = ArgumentTones.Arrogant) =>
            new(speech, fallacyId, premises ?? ValidPremises, weakPremise, tone);
    }
}
