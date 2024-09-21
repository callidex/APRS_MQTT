using System.ComponentModel.DataAnnotations;


namespace APRS_MQTT.Models
{
    public class APRSData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Callsign { get; set; }

        [Required]
        public string NodeId { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
