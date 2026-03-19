namespace OneJevelsCompany.Web.Models.Home
{
    public class HomeCollectionCardViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public int ComponentsCount { get; set; }
        public string? PreviewImageUrl { get; set; }
    }
}
