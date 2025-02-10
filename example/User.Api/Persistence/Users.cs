using System.ComponentModel.DataAnnotations.Schema;

namespace User.Api.Persistence
{
    [Table("Users")]
    public class Users
    {
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("Name", TypeName = "varchar(255)")]
        public string Name { get; set; }

        [Column("Email", TypeName = "varchar(255)")]
        public string Email { get; set; }

        [Column("Age", TypeName = "int")] 
        public int Age { get; set; }
    }
}