using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace defconflix.Models
{
    [Table("files")]
    public class Files
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("file_path")]
        public string File_Path { get; set; }
        [Column("file_name")]
        public string File_Name { get; set; }
        [Column("extension")]
        public string Extension { get; set; }
        [Column("size_bytes")]
        public int Size_Bytes { get; set; }
        [Column("hash")]
        public string Hash { get; set; }
        [Column("status")]
        public string Status { get; set; }
        [Column("created_at")]
        public DateTime Created_At { get; set; }
        [Column("updated_at")]
        public DateTime? Updated_At { get; set; }
    }
}
