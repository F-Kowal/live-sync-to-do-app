using live_sync_to_do_app.Data;
using live_sync_to_do_app.Hubs;
using live_sync_to_do_app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace live_sync_to_do_app.Controllers
{
    public class TodoListsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<TodoHub> _hubContext;

        public TodoListsController(ApplicationDbContext context, IHubContext<TodoHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: TodoLists
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var currentUser = User.Identity.Name;

            var myLists = await _context.TodoLists
                .Include(l => l.Tasks)
                .Where(l => l.OwnerEmail == currentUser || (l.SharedWith != null && l.SharedWith.Contains(currentUser)))
                .OrderByDescending(l => l.Id)
                .ToListAsync();

            return View(myLists);
        }

        // GET: TodoLists/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var todoList = await _context.TodoLists
                .Include(l => l.Tasks)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (todoList == null)
            {
                return NotFound();
            }

            var currentUser = User.Identity.Name;
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            return View(todoList);
        }

        // GET: TodoLists/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: TodoLists/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Id,Name,SharedWith")] TodoList todoList)
        {
            todoList.OwnerEmail = User.Identity.Name;

            ModelState.Remove("OwnerEmail");
            ModelState.Remove("Tasks");
            ModelState.Remove("TodoList");

            if (ModelState.IsValid)
            {
                _context.Add(todoList);
                await _context.SaveChangesAsync();

                // Notify the owner via SignalR
                await _hubContext.Clients.Group($"user_{todoList.OwnerEmail}").SendAsync("ReceiveListCreated", todoList.Id, todoList.Name, todoList.OwnerEmail);

                // Notify shared users via SignalR
                if (!string.IsNullOrEmpty(todoList.SharedWith))
                {
                    var sharedEmails = todoList.SharedWith.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var email in sharedEmails)
                    {
                        await _hubContext.Clients.Group($"user_{email}").SendAsync("ReceiveListShared", todoList.Id, todoList.Name, todoList.OwnerEmail);
                    }
                }

                // Return JSON for AJAX requests
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { id = todoList.Id, name = todoList.Name, ownerEmail = todoList.OwnerEmail });
                }

                return RedirectToAction(nameof(Index));
            }
            return View(todoList);
        }

        // GET: TodoLists/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var todoList = await _context.TodoLists.FindAsync(id);
            if (todoList == null)
            {
                return NotFound();
            }
            return View(todoList);
        }

        // POST: TodoLists/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,OwnerEmail,SharedWith")] TodoList todoList)
        {
            if (id != todoList.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the original list to compare shared users
                    var originalList = await _context.TodoLists.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
                    var originalShared = originalList?.SharedWith ?? "";
                    var newShared = todoList.SharedWith ?? "";

                    var originalName = originalList?.Name ?? todoList.Name;
                    _context.Update(todoList);
                    await _context.SaveChangesAsync();

                    // Broadcast list name change if it changed
                    if (originalName != todoList.Name)
                    {
                        await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveListUpdated", id, todoList.Name);
                    }

                    // Broadcast sharing changes to all users viewing this list
                    await _hubContext.Clients.Group(id.ToString()).SendAsync("ReceiveListSharingUpdated", id, todoList.SharedWith ?? "");

                    // Notify newly shared users
                    if (!string.IsNullOrEmpty(newShared))
                    {
                        var originalEmails = string.IsNullOrEmpty(originalShared) 
                            ? new HashSet<string>() 
                            : new HashSet<string>(originalShared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        
                        var newEmails = newShared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        
                        foreach (var email in newEmails)
                        {
                            if (!originalEmails.Contains(email))
                            {
                                // This is a newly shared user
                                await _hubContext.Clients.Group($"user_{email}").SendAsync("ReceiveListShared", todoList.Id, todoList.Name, todoList.OwnerEmail);
                            }
                        }
                    }

                    // Notify users who were removed from sharing
                    if (!string.IsNullOrEmpty(originalShared))
                    {
                        var originalEmails = new HashSet<string>(originalShared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        var newEmails = string.IsNullOrEmpty(newShared) 
                            ? new HashSet<string>() 
                            : new HashSet<string>(newShared.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        
                        foreach (var email in originalEmails)
                        {
                            if (!newEmails.Contains(email))
                            {
                                // This user was removed from sharing
                                await _hubContext.Clients.Group($"user_{email}").SendAsync("ReceiveListUnshared", id);
                            }
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TodoListExists(todoList.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(todoList);
        }

        // GET: TodoLists/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var todoList = await _context.TodoLists
                .FirstOrDefaultAsync(m => m.Id == id);
            if (todoList == null)
            {
                return NotFound();
            }

            return View(todoList);
        }

        // POST: TodoLists/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var todoList = await _context.TodoLists.FindAsync(id);
            if (todoList != null)
            {
                var listId = todoList.Id;
                var sharedWith = todoList.SharedWith ?? "";
                
                _context.TodoLists.Remove(todoList);
                await _context.SaveChangesAsync();

                // Broadcast list deletion to all users who have access
                await _hubContext.Clients.Group(listId.ToString()).SendAsync("ReceiveListDeleted", listId);
                
                // Also notify shared users via their user groups
                if (!string.IsNullOrEmpty(sharedWith))
                {
                    var sharedEmails = sharedWith.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var email in sharedEmails)
                    {
                        await _hubContext.Clients.Group($"user_{email}").SendAsync("ReceiveListDeleted", listId);
                    }
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool TodoListExists(int id)
        {
            return _context.TodoLists.Any(e => e.Id == id);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddTask(int listId, string title, string? description = null, DateTime? dueDate = null, string? assignedTo = null)
        {
            // Verify user has access to the list
            var currentUser = User.Identity.Name;
            var todoList = await _context.TodoLists.FindAsync(listId);
            if (todoList == null)
            {
                return NotFound();
            }
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            var task = new TodoTask 
            { 
                TodoListId = listId, 
                Title = title, 
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                DueDate = dueDate,
                AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo,
                IsCompleted = false 
            };
            _context.TodoTasks.Add(task);
            await _context.SaveChangesAsync();

            // Broadcast to all users in the list group
            await _hubContext.Clients.Group(listId.ToString()).SendAsync("ReceiveTaskAdded", 
                task.TodoListId, task.Id, task.Title, task.Description ?? "", task.DueDate?.ToString("yyyy-MM-dd") ?? "", task.AssignedTo ?? "");

            return Json(new 
            { 
                id = task.Id, 
                title = task.Title,
                description = task.Description,
                dueDate = task.DueDate?.ToString("yyyy-MM-dd"),
                assignedTo = task.AssignedTo
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ToggleTask(int taskId)
        {
            var task = await _context.TodoTasks.Include(t => t.TodoList).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            // Verify user has access to the list
            var currentUser = User.Identity.Name;
            var todoList = task.TodoList;
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            task.IsCompleted = !task.IsCompleted;
            await _context.SaveChangesAsync();

            // Broadcast to all users in the list group
            await _hubContext.Clients.Group(task.TodoListId.ToString()).SendAsync("ReceiveTaskToggled", taskId, task.IsCompleted);

            return Json(new { id = task.Id, isCompleted = task.IsCompleted });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            var task = await _context.TodoTasks.Include(t => t.TodoList).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            // Verify user has access to the list
            var currentUser = User.Identity.Name;
            var todoList = task.TodoList;
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            var listId = task.TodoListId;
            _context.TodoTasks.Remove(task);
            await _context.SaveChangesAsync();

            // Broadcast to all users in the list group
            await _hubContext.Clients.Group(listId.ToString()).SendAsync("ReceiveTaskDeleted", listId, taskId);

            return Json(new { listId = listId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateTask(int taskId, string title, string? description = null, string? dueDate = null, string? assignedTo = null)
        {
            var task = await _context.TodoTasks.Include(t => t.TodoList).FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null) return NotFound();

            // Verify user has access to the list
            var currentUser = User.Identity.Name;
            var todoList = task.TodoList;
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            // Always update title if provided
            if (!string.IsNullOrWhiteSpace(title))
            {
                task.Title = title;
            }

            // Handle description - null means don't change, empty string means clear
            if (description != null)
            {
                task.Description = string.IsNullOrWhiteSpace(description) ? null : description;
            }

            // Handle due date - null means don't change, empty string means clear
            if (dueDate != null)
            {
                if (string.IsNullOrWhiteSpace(dueDate))
                {
                    task.DueDate = null;
                }
                else if (DateTime.TryParse(dueDate, out var parsedDate))
                {
                    task.DueDate = parsedDate;
                }
            }

            // Handle assigned to - null means don't change, empty string means clear
            if (assignedTo != null)
            {
                task.AssignedTo = string.IsNullOrWhiteSpace(assignedTo) ? null : assignedTo;
            }

            await _context.SaveChangesAsync();

            // Broadcast to all users in the list group
            await _hubContext.Clients.Group(task.TodoListId.ToString()).SendAsync("ReceiveTaskUpdated", 
                task.Id, task.Title, task.Description ?? "", task.DueDate?.ToString("yyyy-MM-dd") ?? "", task.AssignedTo ?? "", task.IsCompleted);

            return Json(new 
            { 
                id = task.Id, 
                title = task.Title,
                description = task.Description,
                dueDate = task.DueDate?.ToString("yyyy-MM-dd"),
                assignedTo = task.AssignedTo,
                isCompleted = task.IsCompleted
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetList(int id)
        {
            var todoList = await _context.TodoLists
                .Include(l => l.Tasks)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (todoList == null)
            {
                return NotFound();
            }

            var currentUser = User.Identity.Name;
            if (todoList.OwnerEmail != currentUser && !(todoList.SharedWith ?? "").Contains(currentUser))
            {
                return Forbid();
            }

            return Json(new
            {
                id = todoList.Id,
                name = todoList.Name,
                ownerEmail = todoList.OwnerEmail,
                sharedWith = todoList.SharedWith,
                tasks = todoList.Tasks.Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    description = t.Description,
                    dueDate = t.DueDate?.ToString("yyyy-MM-dd"),
                    isCompleted = t.IsCompleted,
                    assignedTo = t.AssignedTo
                }).ToList()
            });
        }
    }
}
