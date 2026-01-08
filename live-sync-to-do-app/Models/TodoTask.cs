using System;
using System.ComponentModel.DataAnnotations;

namespace live_sync_to_do_app.Models
{
    public class TodoTask
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; }

        public string? Description { get; set; }

        public DateTime? DueDate { get; set; }

        public bool IsCompleted { get; set; }

        public string? AssignedTo { get; set; }

        public int TodoListId { get; set; }
        public virtual TodoList TodoList { get; set; }
    }
}
