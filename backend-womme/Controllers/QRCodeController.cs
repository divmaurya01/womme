using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using QRCoder;
using System.Text;

namespace WommeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QRCodeController : ControllerBase
    {
        public class JobDto
        {
            public string? Job { get; set; }
            public string? QRType { get; set; } // Added
        }


        [HttpPost("GenerateQrWithJob")]
        public IActionResult GenerateQrWithJob([FromBody] JobDto request)  
        {
            if (string.IsNullOrEmpty(request.Job))   
            {
                return BadRequest("Job cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(request.QRType))
            {
                return BadRequest("QRType cannot be empty.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"qrType: {request.QRType}");
            sb.AppendLine($"job: {request.Job}");  
            var qrData = sb.ToString();

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(5);

            return File(qrCodeImage, "image/png");
        }


        [HttpPost("GenerateQrWithOperation")]
        public IActionResult GenerateQrWithOperation([FromBody] QrOperationRequest request) 
        {
            if (!request.OperNum.HasValue)
            {
                return BadRequest("OperNum cannot be null.");
            }

            if (string.IsNullOrEmpty(request.QRType))
            {
                return BadRequest("QRType cannot be empty.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"qrType: {request.QRType}");
            sb.AppendLine($"operNum: {request.OperNum}");
            var qrData = sb.ToString();

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(5);

            return File(qrCodeImage, "image/png");
        }

        public class QrOperationRequest
        {
            public int? OperNum { get; set; }
            public string? QRType { get; set; }
        }
        

        [HttpPost("GenerateQrWithEmployee")]  
        public IActionResult GenerateQrWithEmployee([FromBody] QrEmployeeRequest request)
        {
            
            if (string.IsNullOrEmpty(request.EmpNum))
             {  
                 return BadRequest("EmpNum cannot be null or empty.");
             }


            if (string.IsNullOrEmpty(request.QRType))
            {
                return BadRequest("QRType cannot be empty.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"qrType: {request.QRType}");
            sb.AppendLine($"empNum: {request.EmpNum}");
            var qrData = sb.ToString();

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(5);

            return File(qrCodeImage, "image/png");
        }

        public class QrEmployeeRequest
        {
            public string? EmpNum { get; set; }
            public string? QRType { get; set; }
        }



        [HttpPost("GenerateQrWithMachine")]
        public IActionResult GenerateQrWithMachine([FromBody] QrMachineRequest request)
        {
            if (string.IsNullOrEmpty(request.MachineNumber))
            {
                return BadRequest("MachineNumber cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(request.QRType))
            {
                return BadRequest("QRType cannot be empty.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"qrType: {request.QRType}");
            sb.AppendLine($"machineNumber: {request.MachineNumber}");
            var qrData = sb.ToString();

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(5);

            return File(qrCodeImage, "image/png");
        }

        public class QrMachineRequest
        {
            public string? MachineNumber { get; set; }
            public string? QRType { get; set; } 
        }


    }
}

 
 