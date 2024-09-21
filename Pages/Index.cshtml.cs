using APRS_MQTT.Data;
using APRS_MQTT.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string Callsign { get; set; }

    [BindProperty]
    public string NodeId { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public bool IsPost { get; set; }

    public void OnGet()
    {
        IsPost = false;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsPost = true;

        if (ModelState.IsValid)
        {
            var aprsData = new APRSData
            {
                Callsign = Callsign,
                NodeId = NodeId,
                Password = Password
            };

            _context.APRSRecords.Add(aprsData);
            await _context.SaveChangesAsync();

            // Optional: Reset the form fields after saving
            Callsign = string.Empty;
            NodeId = string.Empty;
            Password = string.Empty;
        }

        return Page();
    }
}
