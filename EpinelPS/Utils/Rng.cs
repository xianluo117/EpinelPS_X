using EpinelPS.Data;

namespace EpinelPS.Utils
{
    public class Rng
    {
        private static readonly Random random = new();

        public static int Next(int minValue, int maxValue)
        {
            return random.Next(minValue, maxValue);
        }

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string([.. Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)])]);
        }

        public static int RandomId()
        {
            return random.Next();
        } 
        
        /// <summary>
          /// Picks a random item. weights is a list of numbers which represents probability, table Ids represent ID for weight
          /// </summary>
          /// <param name="weights"></param>
          /// <param name="tableIds"></param>
          /// <returns></returns>
          /// <exception cref="Exception"></exception>
        public static ItemRandomRecord PickWeightedItem(ItemRandomRecord[] records)
        {
            int totalWeight = 0;
            foreach (ItemRandomRecord item in records)
                totalWeight += item.Ratio;

            int randomNumber = random.Next(0, totalWeight);

            int runningSum = 0;
            for (int i = 0; i < records.Length; i++)
            {
                runningSum += records[i].Ratio;
                if (randomNumber < runningSum)
                    return records[i];
            }

            throw new Exception("Weight distribution error.");
        }

        /// <summary>
        /// 获取随机装饰物品（自动跳过用户已拥有的物品）
        /// </summary>
        /// <param name="records">奖励列表</param>
        /// <param name="user">用户信息</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static ItemRandomRecord PickProfileItem(ItemRandomRecord[] records, User user)
        {
            // 参数校验
            if (records == null || records.Length == 0)
                throw new ArgumentException("奖励列表不能为空");

            if (user == null)
                throw new ArgumentException("用户信息不能为空");

            // 确保用户的列表不为null
            user.BackgroundList ??= new List<int>();
            user.StickerList ??= new List<int>();

            // 创建可用物品列表（过滤掉用户已拥有的物品）
            var availableRecords = records.Where(record =>
                !user.BackgroundList.Contains(record.RewardId) &&
                !user.StickerList.Contains(record.RewardId)
            ).ToArray();

            // 如果没有可用的物品，抛出异常
            if (availableRecords.Length == 0)
                throw new Exception("用户已拥有所有可抽取的装饰物品");

            // 计算总权重
            int totalWeight = 0;
            foreach (ItemRandomRecord item in availableRecords)
            {
                totalWeight += item.Ratio;
            }

            // 随机选择
            int randomNumber = random.Next(0, totalWeight);

            int runningSum = 0;
            for (int i = 0; i < availableRecords.Length; i++)
            {
                runningSum += availableRecords[i].Ratio;
                if (randomNumber < runningSum)
                    return availableRecords[i];
            }

            throw new Exception("权重分布错误");
        }



    }
}
