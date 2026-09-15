using System;
using System.Linq;
using Game.Core;
using Game.EditorTools;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.UI
{
    public sealed class JurorMeterTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ReferenceVerdicts_DecideEachEnding()
        {
            Assert.AreEqual(251, TribunalVerdicts.Majority);
            Assert.IsTrue(new JurorCount(TribunalVerdicts.SophistAcquittalVotes, true, 1).Acquitted);
            Assert.IsFalse(new JurorCount(TribunalVerdicts.Jurors - TribunalVerdicts.PhilosopherCondemnationVotes, false, 1).Acquitted);
            Assert.IsTrue(new JurorCount(TribunalVerdicts.MasterAcquittalVotes, true, 1).Acquitted);
        }

        [TestCase(340, true, 7)]
        [TestCase(240, false, 11)]
        [TestCase(281, false, 3)]
        public void Count_KeepsTheTotalsAndLeavesMenexenoForLast(int acquittals, bool menexenoAcquits, int seed)
        {
            var count = new JurorCount(acquittals, menexenoAcquits, seed);

            Assert.AreEqual(acquittals, Enumerable.Range(0, TribunalVerdicts.Jurors).Count(count.VoteAt));
            Assert.AreEqual(menexenoAcquits, count.VoteAt(JurorCount.MenexenoIndex));
            Assert.AreEqual(TribunalVerdicts.Jurors - acquittals, count.CondemnationVotes);
        }

        [Test]
        public void Count_RevealsVoteByVoteAndPausesBeforeMenexeno()
        {
            var count = new JurorCount(TribunalVerdicts.SophistAcquittalVotes, true, 5, 100f);

            Assert.AreEqual(10, count.Tick(0.105f));
            count.Tick(4.9f);
            Assert.AreEqual(JurorCount.MenexenoIndex, count.Revealed);
            Assert.IsTrue(count.IsWaitingForMenexeno);

            count.Tick(1f);
            Assert.IsFalse(count.IsComplete);

            count.Tick(JurorCount.MenexenoPauseSeconds);
            Assert.IsTrue(count.IsComplete);
            Assert.AreEqual(TribunalVerdicts.SophistAcquittalVotes, count.AcquittalsRevealed);
            Assert.AreEqual(0, count.Tick(1f));
        }

        [Test]
        public void Count_RejectsAMenexenoVoteThatCannotExist()
        {
            Assert.Throws<ArgumentException>(() => new JurorCount(0, true, 1));
            Assert.Throws<ArgumentException>(() => new JurorCount(TribunalVerdicts.Jurors, false, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new JurorCount(TribunalVerdicts.Jurors + 1, true, 1));
        }

        [TestCase(0)]
        [TestCase(240)]
        [TestCase(281)]
        [TestCase(340)]
        [TestCase(501)]
        public void Layout_FitsBothSidesInsideTheMeterWithoutCrossing(int acquittals)
        {
            var condemnations = TribunalVerdicts.Jurors - acquittals;
            var acquittalPixels = Enumerable.Range(0, acquittals).Select(JurorLayout.AcquittalPixel).ToList();
            var condemnationPixels = Enumerable.Range(0, condemnations).Select(JurorLayout.CondemnationPixel).ToList();

            foreach (var pixel in acquittalPixels.Concat(condemnationPixels))
            {
                Assert.That(pixel.x, Is.InRange(0, JurorLayout.Width - 1));
                Assert.That(pixel.y, Is.InRange(0, JurorLayout.Height - 1));
            }

            if (acquittals > 0 && condemnations > 0)
            {
                Assert.Less(acquittalPixels.Max(pixel => pixel.x), condemnationPixels.Min(pixel => pixel.x));
            }

            Assert.AreEqual(TribunalVerdicts.Jurors, acquittalPixels.Concat(condemnationPixels).Distinct().Count());
        }

        [Test]
        public void View_PaintsEachVoteOnItsSideAndHighlightsMenexeno()
        {
            host = new GameObject("jurors", typeof(RectTransform));
            var view = host.AddComponent<JurorMeterView>();
            var image = new GameObject("meter", typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(host.transform, false);
            view.Configure(image);

            var count = new JurorCount(TribunalVerdicts.SophistAcquittalVotes, true, 9, 1000f);
            view.Bind(count);
            Assert.AreSame(view.Texture, image.texture);
            Assert.AreEqual(FilterMode.Point, view.Texture.filterMode);
            Assert.AreEqual((Color32)view.TrackColor, (Color32)view.PixelAt(0, JurorLayout.FirstRowY));

            view.Advance(0.0015f);
            var first = count.VoteAt(0) ? JurorLayout.AcquittalPixel(0) : JurorLayout.CondemnationPixel(0);
            Assert.AreEqual((Color32)(count.VoteAt(0) ? view.AcquittalColor : view.CondemnationColor), (Color32)view.PixelAt(first.x, first.y));

            view.SkipToEnd();
            Assert.IsTrue(view.MenexenoPixel.HasValue);
            var menexeno = view.MenexenoPixel.Value;
            Assert.AreEqual((Color32)view.MenexenoColor, (Color32)view.PixelAt(menexeno.x, menexeno.y));
            Assert.AreEqual((Color32)view.MenexenoColor, (Color32)view.PixelAt(menexeno.x, JurorLayout.MarkerLowY));

            var acquittalPixels = 0;
            for (var x = 0; x < JurorLayout.Width; x++)
            {
                for (var y = 0; y < JurorLayout.Height; y++)
                {
                    if ((Color32)view.PixelAt(x, y) is var color && color.Equals((Color32)view.AcquittalColor))
                    {
                        acquittalPixels++;
                    }
                }
            }

            Assert.AreEqual(TribunalVerdicts.SophistAcquittalVotes - 1, acquittalPixels);
        }

        [Test]
        public void Prefab_HoldsA200By16MeterWithItsView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleHudTools.JurorMeterPrefabPath);
            Assert.IsNotNull(prefab, $"run BattleHudTools.BuildHud to create {BattleHudTools.JurorMeterPrefabPath}");

            var raw = prefab.GetComponentInChildren<RawImage>(true);
            Assert.IsNotNull(prefab.GetComponentInChildren<JurorMeterView>(true));
            Assert.AreEqual(new Vector2(JurorLayout.Width, JurorLayout.Height), raw.rectTransform.sizeDelta);
        }
    }
}
