using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_385.Data;

namespace Project_385.ViewComponents
{
    public class BrandsViewComponent : ViewComponent
    {
        private readonly DataContext _datacontext;
        public BrandsViewComponent(DataContext context)
        {
            _datacontext = context;
        }
        public async Task<IViewComponentResult> InvokeAsync() =>View(await _datacontext.Brands.Where(x => x.Status == 1).ToListAsync());
    }
}
