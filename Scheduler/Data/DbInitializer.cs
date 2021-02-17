using Scheduler.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scheduler.Areas.Identity.Data
{
    public static class DbInitializer
    {
        public static void Initialize(SchedulerDbContext context)
        {
            context.Database.EnsureCreated();

            if (context.Users.Any())
            {
                return;
            }
        }
    }
}
