using NUnit.Framework;
using NUnit.Framework.Internal;

namespace MyTests
{
    public class UnitTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.AreEqual(1 + 1, 2, "An old contract people tell their kids about just failed. Math is useless from now on.");
        }
    }
}