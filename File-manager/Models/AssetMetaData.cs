namespace File_manager.Models
{
    public class AssetMetadata
    {
        /// <summary>Час останньої модифікації файлу на момент реєстрації (для порівняння змін)</summary>
        public DateTime RegisteredTime { get; set; }

        /// <summary>Розмір файлу на момент реєстрації (для порівняння змін)</summary>
        public long RegisteredSize { get; set; }

        /// <summary>Час першого додавання файлу в програму (для визначення статусу New)</summary>
        public DateTime FirstSeenTime { get; set; } = DateTime.Now;

        public List<string> Tags { get; set; } = new();
    }
}