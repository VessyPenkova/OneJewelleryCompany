namespace OneJevelsCompany.Web.Models.Admin
{
    public class CategoryRowViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public int Components { get; set; }
    }
}