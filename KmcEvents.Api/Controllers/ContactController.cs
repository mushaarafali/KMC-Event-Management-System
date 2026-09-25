using KmcEvents.Api.Data;
using KmcEvents.Api.DTOs;
using KmcEvents.Api.Models;
using Microsoft.AspNetCore.Mvc;
namespace KmcEvents.Api.Controllers;
[ApiController,Route("api/contact")]
public class ContactController(AppDbContext db):ControllerBase
{
    [HttpPost] public async Task<IActionResult> Send(ContactDto d){db.ContactMessages.Add(new ContactMessage{Name=d.Name.Trim(),Email=d.Email.Trim(),Subject=d.Subject.Trim(),Message=d.Message.Trim()});await db.SaveChangesAsync();return Ok(new{message="Your message has been sent to KMC."});}
}
