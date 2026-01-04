using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace live_sync_to_do_app.Models
{
    public class TodoList
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string? OwnerEmail { get; set; }

        public string? SharedWith { get; set; }

        public virtual ICollection<TodoTask> Tasks { get; set; } = new List<TodoTask>();
    }
}
