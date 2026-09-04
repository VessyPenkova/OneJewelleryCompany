using System.ComponentModel.DataAnnotations;

namespace OneJevelsCompany.Web.Models.Admin
{
    public class CategoryEditViewModel
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; } = 100;
    }
}