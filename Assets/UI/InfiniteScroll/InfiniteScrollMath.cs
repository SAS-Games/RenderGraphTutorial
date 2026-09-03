namespace SAS.UI.InfiniteScroll
{
    public static class InfiniteScrollMath
    {
        public static int Mod(int value, int count)
        {
            if (count <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(count), "Modulo count must be greater than zero.");

            int result = value % count;
            return result < 0 ? result + count : result;
        }

        public static int Mod(long value, int count)
        {
            if (count <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(count), "Modulo count must be greater than zero.");

            long result = value % count;
            return (int)(result < 0 ? result + count : result);
        }
    }
}