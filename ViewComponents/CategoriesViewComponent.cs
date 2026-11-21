using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Data;

namespace Project_385.ViewComponents
{
    public class CategoriesViewComponent : ViewComponent
    {
        private readonly DataContext _datacontext;
        public CategoriesViewComponent(DataContext context)
        {
            _datacontext = context;
        }
        public async Task<IViewComponentResult> InvokeAsync() =>View(await _datacontext.Categories.Where(x => x.Status == 1).ToListAsync());

    }
}
