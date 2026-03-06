using EpinelPS.Database;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace EpinelPS.Database
{

    

    public class SqliteQueryHelper
    {
        public static string adbPath = AppDomain.CurrentDomain.BaseDirectory + "/base.db";
        public static SqliteConnection _connection;

        private readonly string _connectionString;

        
        
        // C# 实体类
        public class CurrentShopInfo
        {
            public int ShopCategory { get; set; }
            public int ShopTid { get; set; }
            public int RenewCount { get; set; }
            public long RenewAt { get; set; }
            public long NextRenewAt { get; set; }
            public int FreeRenewCount { get; set; }
            public int ProductId { get; set; }
            public int ProductOrder { get; set; }
            public int BuyLimitCount { get; set; }
            public int BuyCount { get; set; }
            public int CorporationType { get; set; }
            public int Discount { get; set; }
            public long EndAt { get; set; }
            public bool UseDateCondition { get; set; }
        }

        public class ShopDate
        {
            public int ShopCategory { get; set; }
            public int LastDay { get; set; }
        }

        /// <summary>
            /// 初始化SQLite查询助手
            /// </summary>
            /// <param name="dbPath">数据库文件路径</param>
            public SqliteQueryHelper(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public static void InitializeDatabase()
        {

            // 确保目录存在
            var directory = Path.GetDirectoryName(adbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connection = new SqliteConnection($"Data Source={adbPath}");
            _connection.Open();

            using var command = _connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS CurrentShopInfo (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,           -- 用户ID                    
                    ShopCategory INTEGER,              -- 商店分类
                    ShopTid INTEGER,                   -- 商店模板ID
                    RenewCount INTEGER,                -- 刷新次数
                    RenewAt INTEGER,                   -- 刷新时间
                    NextRenewAt INTEGER,               -- 下次刷新时间
                    FreeRenewCount INTEGER,            -- 免费刷新次数
                    ProductId INTEGER,                 -- 商品ID
                    ProductOrder INTEGER,              -- 商品排序
                    BuyLimitCount INTEGER,             -- 购买限制次数
                    BuyCount INTEGER,                  -- 已购买次数
                    CorporationType INTEGER,           -- 公司类型
                    Discount INTEGER,                  -- 折扣
                    EndAt INTEGER,                     -- 结束时间
                    UseDateCondition BOOLEAN,          -- 是否使用日期条件
                    PRIMARY KEY (Id)
            );
            CREATE TABLE IF NOT EXISTS ShopDate (
                    ShopCategory INTEGER NOT NULL,           -- 商店类别
                    LastDay INTEGER,                   -- 更新时间
                    PRIMARY KEY (ShopCategory)
            );

            CREATE INDEX IF NOT EXISTS IX_ProductId ON CurrentShopInfo(ProductId);           
        ";

            command.ExecuteNonQuery();
        }

        

        /// <summary>
        /// 执行查询并返回DataTable
        /// </summary>
        /// <param name="sql">SQL查询语句</param>
        /// <param name="parameters">参数数组（可选）</param>
        /// <returns>包含查询结果的DataTable</returns>
        public DataTable ExecuteQuery(string sql, params SqliteParameter[] parameters)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = new SqliteCommand(sql, connection);

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            var dataTable = new DataTable();
            using var reader = command.ExecuteReader();
            dataTable.Load(reader);

            return dataTable;
        }

        /// <summary>
        /// 检查记录是否存在
        /// </summary>
        /// <param name="sql">SQL查询语句</param>
        /// <param name="parameters">参数数组（可选）</param>
        /// <returns>是否存在符合条件的数据</returns>
        public bool Exists(string sql, params SqliteParameter[] parameters)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = new SqliteCommand(sql, connection);

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            using var reader = command.ExecuteReader();
            return reader.HasRows;
        }

        /// <summary>
        /// 获取记录数量
        /// </summary>
        /// <param name="sql">SQL查询语句</param>
        /// <param name="parameters">参数数组（可选）</param>
        /// <returns>符合条件的记录数量</returns>
        public int GetCount(string sql, params SqliteParameter[] parameters)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = new SqliteCommand(sql, connection);

            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            var result = command.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 创建字符串参数
        /// </summary>
        public static SqliteParameter CreateStringParam(string name, string value)
        {
            return new SqliteParameter(name, SqliteType.Text) { Value = value ?? (object)DBNull.Value };
        }

        /// <summary>
        /// 创建整数参数
        /// </summary>
        public static SqliteParameter CreateIntParam(string name, int value)
        {
            return new SqliteParameter(name, SqliteType.Integer) { Value = value };
        }

        /// <summary>
        /// 创建浮点数参数
        /// </summary>
        public static SqliteParameter CreateDoubleParam(string name, double value)
        {
            return new SqliteParameter(name, SqliteType.Real) { Value = value };
        }

        /// <summary>
        /// 创建布尔参数
        /// </summary>
        public static SqliteParameter CreateBoolParam(string name, bool value)
        {
            return new SqliteParameter(name, SqliteType.Integer) { Value = value ? 1 : 0 };
        }

        /// <summary>
        /// 创建日期时间参数
        /// </summary>
        public static SqliteParameter CreateDateTimeParam(string name, DateTime value)
        {
            return new SqliteParameter(name, SqliteType.Text) { Value = value.ToString("yyyy-MM-dd HH:mm:ss") };
        }

    }

    public static class DataTableExtensions
    {
        /// <summary>
        /// 将单列DataTable转换为字符串数组
        /// </summary>
        public static string[] ToStringArray(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return Array.Empty<string>();

            return dataTable.AsEnumerable()
                .Select(row => row[columnIndex].ToString())
                .ToArray();
        }

        /// <summary>
        /// 将单列DataTable转换为字符串列表
        /// </summary>
        public static List<string> ToStringList(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return new List<string>();

            return dataTable.AsEnumerable()
                .Select(row => row[columnIndex].ToString())
                .ToList();
        }

        /// <summary>
        /// 将单列DataTable转换为整数数组
        /// </summary>
        public static int[] ToIntArray(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return Array.Empty<int>();

            return dataTable.AsEnumerable()
                .Select(row => Convert.ToInt32(row[columnIndex]))
                .ToArray();
        }

        /// <summary>
        /// 将单列DataTable转换为整数列表
        /// </summary>
        public static List<int> ToIntList(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return new List<int>();

            return dataTable.AsEnumerable()
                .Select(row => Convert.ToInt32(row[columnIndex]))
                .ToList();
        }

        /// <summary>
        /// 将单列DataTable转换为指定类型的数组
        /// </summary>
        public static T[] ToArray<T>(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return Array.Empty<T>();

            return dataTable.AsEnumerable()
                .Select(row => (T)Convert.ChangeType(row[columnIndex], typeof(T)))
                .ToArray();
        }

        /// <summary>
        /// 将单列DataTable转换为指定类型的列表
        /// </summary>
        public static List<T> ToList<T>(this DataTable dataTable, int columnIndex = 0)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
                return new List<T>();

            return dataTable.AsEnumerable()
                .Select(row => (T)Convert.ChangeType(row[columnIndex], typeof(T)))
                .ToList();
        }

        /// <summary>
        /// 获取第一列的值数组
        /// </summary>
        public static string[] GetFirstColumnValues(this DataTable dataTable)
        {
            return dataTable.ToStringArray(0);
        }
    }

}
