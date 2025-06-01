namespace FrontEdu.Models.Chat
{
    public class ChatUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string StudentGroup { get; set; }

        public string Initials => string.Join("", FullName.Split(' ').Take(2).Select(s => s[0]));
        public Color RoleColor => Role switch
        {
            "Teacher" => Application.Current.Resources["Secondary"] as Color ?? Colors.Blue,
            "Student" => Application.Current.Resources["Tertiary"] as Color ?? Colors.Green,
            "Parent" => Application.Current.Resources["Gray500"] as Color ?? Colors.Orange,
            "Administrator" => Application.Current.Resources["Primary"] as Color ?? Colors.Red,
            _ => Application.Current.Resources["Gray400"] as Color ?? Colors.Gray
        };
    }
}