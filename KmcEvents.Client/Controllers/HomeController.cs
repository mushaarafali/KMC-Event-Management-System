using KmcEvents.Client.Models;
using KmcEvents.Client.Services;
using Microsoft.AspNetCore.Mvc;
namespace KmcEvents.Client.Controllers;
public class HomeController(ApiClient api):Controller
{
    public async Task<IActionResult> Index()=>View((await api.GetAsync<List<EventVm>>("events"))?.Take(6).ToList()??[]);
    public IActionResult About()=>View();
    [HttpGet] public IActionResult Contact()=>View(new ContactVm());
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Contact(ContactVm vm)
    {
        if(!ModelState.IsValid)return View(vm);
        var r=await api.SendAsync<ApiMessage>(HttpMethod.Post,"contact",vm);
        TempData[r.ok?"Success":"Error"]=r.message;
        if(r.ok)return RedirectToAction(nameof(Contact));
        return View(vm);
    }
}
