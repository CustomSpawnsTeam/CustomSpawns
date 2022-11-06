namespace CustomSpawns.Data.Reader
{
    public interface IDataReader<T>
    {
        T Data
        {
            get;
        }
    }
}