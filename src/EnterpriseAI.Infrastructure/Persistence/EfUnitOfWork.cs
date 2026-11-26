using EnterpriseAI.Application.Abstractions.Persistence;

namespace EnterpriseAI.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly EnterpriseAiDbContext _dbContext;

    public EfUnitOfWork(EnterpriseAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}