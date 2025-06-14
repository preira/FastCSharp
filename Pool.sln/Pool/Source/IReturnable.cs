namespace FastCSharp.Pool;

internal interface IReturnable<T>
where T : class, IDisposable
{
    Task<bool> ReturnAsync(Individual<T> individual);
}
