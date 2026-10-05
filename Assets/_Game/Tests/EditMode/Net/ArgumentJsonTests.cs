using Game.Core;
using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode.Net
{
    public sealed class ArgumentJsonTests
    {
        private const string ValidJson = @"{
            ""speech"": ""Todo homem justo é temido pelos injustos. Sócrates era temido. Logo, Sócrates era justo."",
            ""fallacy_id"": ""affirming_the_consequent"",
            ""premises"": [""Todo justo é temido pelos injustos"", ""Sócrates era temido""],
            ""weak_premise"": 1,
            ""tone"": ""arrogant""
        }";

        private ArgumentValidator validator;
        private Argument fallback;

        [SetUp]
        public void SetUp()
        {
            validator = new ArgumentValidator(new[] { "affirming_the_consequent" });
            fallback = new Argument(
                "Argumento de reserva escrito à mão, sempre disponível sem rede.",
                "affirming_the_consequent",
                new[] { "premissa de reserva" },
                0,
                ArgumentTones.Cunning);
        }

        [Test]
        public void TryParse_ReadsEveryField()
        {
            Assert.IsTrue(ArgumentJson.TryParse(ValidJson, validator, out var argument, out var violation));
            Assert.AreEqual(ArgumentViolation.None, violation);
            Assert.AreEqual("affirming_the_consequent", argument.FallacyId);
            Assert.AreEqual(2, argument.Premises.Count);
            Assert.AreEqual(1, argument.WeakPremise);
            Assert.AreEqual(ArgumentTones.Arrogant, argument.Tone);
            StringAssert.StartsWith("Todo homem justo", argument.Speech);
        }

        [Test]
        public void TryParse_AcceptsExplicitNullFallacy()
        {
            var json = ValidJson.Replace(@"""affirming_the_consequent""", "null");

            Assert.IsTrue(ArgumentJson.TryParse(json, validator, out var argument, out _));
            Assert.IsFalse(argument.IsRefutable);
        }

        [Test]
        public void TryParse_ReadsANullWeakPremiseAsAnErrorInTheReasoning()
        {
            var json = ValidJson.Replace(@"""weak_premise"": 1", @"""weak_premise"": null");

            Assert.IsTrue(ArgumentJson.TryParse(json, validator, out var argument, out _));
            Assert.IsTrue(argument.IsOnlyRefutable);
            Assert.IsTrue(ArgumentJson.TryParse(ArgumentJson.Serialize(argument), validator, out var restored, out _));
            Assert.AreEqual(Argument.NoWeakPremise, restored.WeakPremise, "the missing weak premise survives a round trip");
            StringAssert.Contains(@"""weak_premise"":null", ArgumentJson.Serialize(argument).Replace(" ", string.Empty));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase(@"{""speech"": ""sem o resto dos campos obrigatórios do contrato""}")]
        [TestCase(@"{""speech"": ""Todo homem justo é temido pelos injustos, portanto."", ""premises"": [""a""], ""weak_premise"": 0, ""tone"": ""calm""}")]
        [TestCase(@"{""speech"": ""Todo homem justo é temido pelos injustos, portanto."", ""fallacy_id"": null, ""premises"": [""a""], ""weak_premise"": ""um"", ""tone"": ""calm""}")]
        public void TryParse_RejectsMalformedPayloads(string json)
        {
            Assert.IsFalse(ArgumentJson.TryParse(json, validator, out var argument, out var violation));
            Assert.AreEqual(ArgumentViolation.Malformed, violation);
            Assert.IsNull(argument.Speech);
        }

        [Test]
        public void TryParse_RejectsPayloadsThatBreakTheRules()
        {
            var unknownFallacy = ValidJson.Replace("affirming_the_consequent", "made_up_fallacy");
            var badIndex = ValidJson.Replace(@"""weak_premise"": 1", @"""weak_premise"": 5");

            Assert.IsFalse(ArgumentJson.TryParse(unknownFallacy, validator, out _, out var unknownViolation));
            Assert.AreEqual(ArgumentViolation.UnknownFallacy, unknownViolation);
            Assert.IsFalse(ArgumentJson.TryParse(badIndex, validator, out _, out var indexViolation));
            Assert.AreEqual(ArgumentViolation.WeakPremiseOutOfRange, indexViolation);
        }

        [TestCase("{}")]
        [TestCase("null")]
        [TestCase(@"{""speech"": ""curta"", ""fallacy_id"": null, ""premises"": [""a""], ""weak_premise"": 0, ""tone"": ""calm""}")]
        public void ParseOrFallback_NeverLetsAnInvalidPayloadThrough(string json)
        {
            var result = ArgumentJson.ParseOrFallback(json, validator, fallback);

            Assert.AreEqual(fallback.Speech, result.Speech);
            Assert.AreEqual(fallback.Tone, result.Tone);
        }

        [Test]
        public void ParseValidList_KeepsOnlyValidItems()
        {
            var json = "[" + ValidJson + @", {""speech"": ""curta""}, 42, " + ValidJson.Replace(@"""weak_premise"": 1", @"""weak_premise"": 9") + ", " + ValidJson + "]";
            var accepted = new System.Collections.Generic.List<Argument>();

            Assert.AreEqual(2, ArgumentJson.ParseValidList(json, validator, accepted));
            Assert.AreEqual(2, accepted.Count);
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("not json")]
        public void ParseValidList_WithoutAnArray_AcceptsNothing(string json)
        {
            var accepted = new System.Collections.Generic.List<Argument>();

            Assert.AreEqual(0, ArgumentJson.ParseValidList(json, validator, accepted));
            Assert.IsEmpty(accepted);
        }

        [Test]
        public void Serialize_RoundTripsThroughTryParse()
        {
            Assert.IsTrue(ArgumentJson.TryParse(ArgumentJson.Serialize(fallback), validator, out var restored, out _));
            Assert.AreEqual(fallback.Speech, restored.Speech);
            CollectionAssert.AreEqual(fallback.Premises, restored.Premises);
        }
    }
}
