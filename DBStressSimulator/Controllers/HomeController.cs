using DBStressSimulator.DB;
using DBStressSimulator.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DBStressSimulator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly CpuStressManager _cpuStressManager;
        private readonly DatabaseReaderService _readerService;

        public HomeController(ILogger<HomeController> logger, ConnectionManager connectionManager, CpuStressManager cpuStressManager, DatabaseReaderService readerService)
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _cpuStressManager = cpuStressManager;
            _readerService = readerService;
        }

        public IActionResult Index()
        {
            ViewBag.Count = _connectionManager.ActiveCount;
            ViewBag.CpuWorkers = _cpuStressManager.ActiveCount;
            return View();
        }

        [HttpPost]
        public IActionResult OpenConnections(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _connectionManager.OpenConnection();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult CloseConnections()
        {
            _connectionManager.CloseAll();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult StartCpu(int count, string mode)
        {
            var testMode = Enum.Parse<StressTestMode>(mode);
            _cpuStressManager.StartWorkers(count, testMode);

            TempData["Message"] = $"Started {count} {testMode} workers";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> StopCpu()
        {
            await _cpuStressManager.StopAll();
            return RedirectToAction("Index");
        }

        public IActionResult Reader()
        {
            return View();
        }

        [HttpPost("start")]
        public IActionResult Start()
        {
            _readerService.StartReading();
            TempData["Message"] = "Reading started successfully";
            return RedirectToAction("Reader");
        }

        [HttpPost("stop")]
        public async Task<IActionResult> Stop()
        {
            await _readerService.StopReading();

            TempData["Message"] = "Reading stopped successfully";
            return RedirectToAction("Reader");            
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
