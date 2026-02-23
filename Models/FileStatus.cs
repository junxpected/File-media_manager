namespace MediaStatusClassifier.Models
{
    /// <summary>
    /// Перелік можливих станів медіа-файлу
    /// </summary>
    public enum FileStatus
    {
        New,          // Щойно знайдений
        InProgress,   // В роботі (є .xmp або відкрито в редакторі)
        Finished,     // Готовий (є рендер)
        Archived      // В архіві
    }
}