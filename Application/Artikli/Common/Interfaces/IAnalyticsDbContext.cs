using Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Artikli.Common.Interfaces
{
    public interface IAnalyticsDbContext 
    {
        public DbSet<ProductsDim> ProductsDim { get; }
        public DbSet<StoresDim> StoresDim { get; }
    }
}
