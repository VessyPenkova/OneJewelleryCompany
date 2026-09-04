namespace OneJevelsCompany.Web.Models.Shop
{
    public class DesignGalleryItem
    {
        public string Title { get; set; } = "";
        public string? ImageUrl { get; set; }    // for built-in designs
        public string? DataUrl { get; set; }     // for custom (data URL)
        public string Category { get; set; } = "Bracelet";
        public int Rating { get; set; }          // popularity
        public string? LinkUrl { get; set; }     // optional details link
        public bool IsCustom { get; set; }
    }
}
