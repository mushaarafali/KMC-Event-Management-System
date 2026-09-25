using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace KmcEvents.Client.Filters;
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method)] public class SessionAuthorizeAttribute(params string[] roles):ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext c)
    {
        var s=c.HttpContext.Session;
        var token=s.GetString("Token");
        var role=s.GetString("Role");
        if(string.IsNullOrEmpty(token))
        {
            c.Result=new RedirectToActionResult("Login","Account",new{returnUrl=c.HttpContext.Request.Path});
            return;
        }
        if(roles.Length>0&&!roles.Contains(role))
        {
            c.Result=new RedirectToActionResult("Denied","Account",null);
        }
    }
}
