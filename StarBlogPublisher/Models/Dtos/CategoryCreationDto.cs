﻿namespace StarBlogPublisher.Models.Dtos;

public record CategoryCreationDto {
    public string Name { get; set; }
    public int ParentId { get; set; }
    public bool Visible { get; set; } = true;
}