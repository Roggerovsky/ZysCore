using System;

namespace DiscordAutomation.API.Models
{
    public abstract class BaseEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Aktualizujemy modele, żeby dziedziczyły po BaseEntity:
    // Guild, Module, Rule już mają te właściwości
}