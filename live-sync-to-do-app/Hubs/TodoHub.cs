using Microsoft.AspNetCore.SignalR;

namespace live_sync_to_do_app.Hubs
{
    public class TodoHub : Hub
    {
        public async Task JoinListGroup(int listId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, listId.ToString());
        }

        public async Task LeaveListGroup(int listId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, listId.ToString());
        }

        public async Task SendTaskAdded(int listId, int taskId, string title, string description, string dueDate)
        {
            await Clients.Group(listId.ToString()).SendAsync("ReceiveTaskAdded", taskId, title, description, dueDate);
        }

        public async Task SendTaskToggled(int listId, int taskId, bool isCompleted)
        {
            await Clients.Group(listId.ToString()).SendAsync("ReceiveTaskToggled", taskId, isCompleted);
        }

        public async Task SendTaskDeleted(int listId, int taskId)
        {
            await Clients.Group(listId.ToString()).SendAsync("ReceiveTaskDeleted", taskId);
        }

        public async Task SendListUpdated(int listId)
        {
            await Clients.Group(listId.ToString()).SendAsync("ReceiveListUpdated", listId);
        }

        public async Task JoinUserGroup(string userEmail)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userEmail}");
        }

        public async Task SendListShared(string userEmail, int listId, string listName, string ownerEmail)
        {
            await Clients.Group($"user_{userEmail}").SendAsync("ReceiveListShared", listId, listName, ownerEmail);
        }
    }
}
