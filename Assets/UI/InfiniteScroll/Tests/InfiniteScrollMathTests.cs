using System;
using NUnit.Framework;

namespace SAS.UI.InfiniteScroll.Tests
{
    public sealed class InfiniteScrollMathTests
    {
        [TestCase(0, 5, 0)]
        [TestCase(7, 5, 2)]
        [TestCase(-1, 5, 4)]
        [TestCase(-7, 5, 3)]
        [TestCase(int.MaxValue - 1, int.MaxValue, int.MaxValue - 1)]
        public void IntModuloAlwaysReturnsCanonicalIndex(int value, int count, int expected)
        {
            Assert.That(InfiniteScrollMath.Mod(value, count), Is.EqualTo(expected));
        }

        [Test]
        public void LongModuloHandlesNegativeLogicalIndices()
        {
            Assert.That(InfiniteScrollMath.Mod(long.MinValue, 7), Is.InRange(0, 6));
        }

        [Test]
        public void ModuloRejectsNonPositiveCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteScrollMath.Mod(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => InfiniteScrollMath.Mod(1L, -1));
        }
    }
}
