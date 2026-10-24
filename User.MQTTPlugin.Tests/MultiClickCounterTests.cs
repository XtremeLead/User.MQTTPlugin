using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace User.MQTTPlugin.Tests
{
    [TestClass]
    public class MultiClickCounterTests
    {
        private const uint DoubleClickTime = 500;

        private readonly MultiClickCounter counter = new MultiClickCounter(DoubleClickTime);
        private readonly object field = new object();
        private readonly object otherField = new object();

        [TestMethod]
        public void Register_ThreeQuickClicksOnSameField_CountsOneTwoThree()
        {
            Assert.AreEqual(1, counter.Register(field, 1000));
            Assert.AreEqual(2, counter.Register(field, 1200));
            Assert.AreEqual(3, counter.Register(field, 1400));
        }

        [TestMethod]
        public void Register_FourthQuickClick_KeepsCounting()
        {
            counter.Register(field, 1000);
            counter.Register(field, 1100);
            counter.Register(field, 1200);

            Assert.AreEqual(4, counter.Register(field, 1300));
        }

        // The interval is measured from the previous click, not from the first.
        [TestMethod]
        public void Register_ClickExactlyAtDoubleClickTime_ContinuesSeries()
        {
            counter.Register(field, 1000);

            Assert.AreEqual(2, counter.Register(field, 1500));
            Assert.AreEqual(3, counter.Register(field, 2000));
        }

        [TestMethod]
        public void Register_ClickAfterDoubleClickTime_StartsNewSeries()
        {
            counter.Register(field, 1000);
            counter.Register(field, 1200);

            Assert.AreEqual(1, counter.Register(field, 1701));
        }

        [TestMethod]
        public void Register_ClickOnOtherField_StartsNewSeries()
        {
            counter.Register(field, 1000);
            counter.Register(field, 1100);

            Assert.AreEqual(1, counter.Register(otherField, 1200));
            Assert.AreEqual(1, counter.Register(field, 1300));
        }

        // MouseEventArgs.Timestamp is a tick count that wraps from int.MaxValue to int.MinValue.
        [TestMethod]
        public void Register_TimestampWrapsAround_ContinuesSeries()
        {
            counter.Register(field, int.MaxValue - 100);

            Assert.AreEqual(2, counter.Register(field, int.MinValue + 100));
        }

        [TestMethod]
        public void Register_FirstClickAtTimestampZero_StartsAtOne()
        {
            Assert.AreEqual(1, counter.Register(field, 0));
            Assert.AreEqual(2, counter.Register(field, 100));
        }
    }
}
