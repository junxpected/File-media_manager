namespace File_manager.Interfaces
{
    // Кожен тип проекту реалізує свої правила ігнорування
    public interface IProjectIgnoreRule
    {
        bool ShouldIgnoreDirectory(string dirName);
    }
}