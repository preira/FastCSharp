namespace FastCSharp.Pool;

internal interface IReturnable<T>
where T : class, IDisposable
{
    bool Return(Individual<T> individual);
}
