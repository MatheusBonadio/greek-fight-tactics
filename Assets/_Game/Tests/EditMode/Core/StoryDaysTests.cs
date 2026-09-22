using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode.Core
{
    public sealed class StoryDaysTests
    {
        private const string OtherAbsence = "eudoro_absent_days";

        [Test]
        public void Pass_CountsTheDaysFromTheFirst()
        {
            var save = new SaveData();
            Assert.AreEqual(StoryDays.FirstDay, StoryDays.DayOf(save));
            Assert.AreEqual(StoryDays.FirstDay, StoryDays.DayOf(null));

            StoryDays.Pass(save);
            StoryDays.Pass(save);

            Assert.AreEqual(StoryDays.FirstDay + 2, StoryDays.DayOf(save));
            Assert.AreEqual("3", save.Flags[StoryDays.DayFlag]);
            Assert.IsTrue(SaveFlags.IsCustomName(StoryDays.DayFlag));
            Assert.Throws<ArgumentNullException>(() => StoryDays.Pass(null));
        }

        [Test]
        public void Pass_CountsDownEveryAbsenceAndClearsItOnTheLastDay()
        {
            var save = new SaveData();
            save.Flags[OtherAbsence] = "2";
            save.Flags["broken_absent_days"] = "soon";
            save.Flags["met_menexeno"] = "true";

            StoryDays.Pass(save);
            Assert.AreEqual(1, StoryDays.AbsenceLeft(save, OtherAbsence));
            Assert.IsFalse(save.Flags.ContainsKey("broken_absent_days"), "an unreadable absence ends at once");

            StoryDays.Pass(save);
            Assert.IsFalse(save.Flags.ContainsKey(OtherAbsence), "an absence that ends leaves no flag behind, so absent conditions pass");
            Assert.AreEqual("true", save.Flags["met_menexeno"], "only absences count down");
            Assert.IsFalse(StoryDays.IsAbsenceFlag(StoryDays.DayFlag));
            Assert.IsFalse(StoryDays.IsAbsenceFlag(StoryDays.AbsenceSuffix));
        }

        [Test]
        public void AcceptingDamaris_KeepsMenexenoAwayForThreeDays()
        {
            var save = new SaveData();
            var menexenoIsBack = StoryCondition.Lacks(DamarisOffer.MenexenoAbsentDaysFlag);
            Assert.IsTrue(StoryDays.IsAbsenceFlag(DamarisOffer.MenexenoAbsentDaysFlag));

            DamarisOffer.Apply(save, DamarisChoice.Accepted);
            for (var day = 1; day < DamarisOffer.MenexenoAbsentDays; day++)
            {
                StoryDays.Pass(save);
                Assert.IsFalse(menexenoIsBack.IsMet(save), $"Menexeno is still away after {day} day(s)");
            }

            StoryDays.Pass(save);
            Assert.IsTrue(menexenoIsBack.IsMet(save), "Menexeno returns on the third day");
            Assert.AreEqual(StoryDays.FirstDay + DamarisOffer.MenexenoAbsentDays, StoryDays.DayOf(save));
        }
    }
}
