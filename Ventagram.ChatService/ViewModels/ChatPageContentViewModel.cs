namespace Ventagram.ChatService.ViewModels;

public class ChatPageContentViewModel
{
    public int CurrentUserId { get; set; }
    public string CurrentUserMessageColor { get; set; } = "rose";
    public List<ChatInboxItemViewModel> Inbox { get; set; } = [];
    public ChatConversationViewModel? SelectedConversation { get; set; }
}
