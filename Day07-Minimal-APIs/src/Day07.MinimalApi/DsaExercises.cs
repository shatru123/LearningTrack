namespace Day07.MinimalApi;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 121 - Best Time to Buy and Sell Stock
    /// You want to maximize your profit by choosing a single day to buy one stock and choosing
    /// a different day in the future to sell that stock.
    /// Time Complexity: O(n) single pass.
    /// Space Complexity: O(1).
    /// </summary>
    public static int MaxProfit(int[] prices)
    {
        if (prices == null || prices.Length < 2)
            return 0;

        int minPrice = int.MaxValue;
        int maxProfit = 0;

        for (int i = 0; i < prices.Length; i++)
        {
            if (prices[i] < minPrice)
            {
                minPrice = prices[i];
            }
            else
            {
                int currentProfit = prices[i] - minPrice;
                if (currentProfit > maxProfit)
                {
                    maxProfit = currentProfit;
                }
            }
        }

        return maxProfit;
    }
}
