using System;

namespace CustomSpawns.Data.Reader
{
    public abstract class AbstractDataReader<T, D> : IDataReader<D> where T : class
    {
        public abstract D Data { get; }
    }
}