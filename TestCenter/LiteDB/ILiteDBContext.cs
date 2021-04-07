using LiteDB;

namespace TestCenter.LiteDb
{
    public interface ILiteDbContext
    {
        LiteDatabase Database { get; }
    }
}