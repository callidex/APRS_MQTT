using APRS_MQTT.Data;
using APRS_MQTT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public class RecordsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public RecordsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<APRSData> APRSRecords { get; set; }

    public async Task OnGetAsync()
    {
        // Fetch all records from the database
        APRSRecords = await _context.APRSRecords.ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        // Find the record by ID
        var record = await _context.APRSRecords.FindAsync(id);

        if (record != null)
        {
            // Remove the record from the database
            _context.APRSRecords.Remove(record);
            await _context.SaveChangesAsync();
        }

        // Redirect to the same page to refresh the list after deletion
        return RedirectToPage();
    }

}
