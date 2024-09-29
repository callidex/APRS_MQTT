namespace Tests
{
    [TestClass]
    public class APRSTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual(APRS.FormatCoordinates(-275644416, 1532100608), "2733.33S/15312.12E");
        }
    }
}