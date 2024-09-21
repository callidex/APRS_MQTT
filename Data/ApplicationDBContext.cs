using APRS_MQTT.Models;
using Microsoft.EntityFrameworkCore;
namespace APRS_MQTT.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<APRSData> APRSRecords { get; set; }
    }

}
