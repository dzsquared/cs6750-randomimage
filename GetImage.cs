using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Company.Function
{
    public static class GetImage
    {
        [FunctionName("GetImage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string imageIdString = req.Query["imageID"];
            // try parse imageId to int
            int imageId;
            if (!int.TryParse(imageIdString, out imageId))
            {
                return new BadRequestObjectResult("Invalid imageID");
            }

            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            // get all image options from the container
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            string containerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME");

            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            List<BlobItem> blobItems = new List<BlobItem>();
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                blobItems.Add(blobItem);
            }

            // confirm imageId is equal to or less than length of blobItems
            if (imageId > blobItems.Count || imageId == 0)
            {
                return new BadRequestObjectResult("imageID higher than total number of images available");
            }

            // scale the current minute to the number of images in the container
            int currentMinute = DateTime.Now.Minute;
            int imageIndex = currentMinute % blobItems.Count;
            imageIndex += imageId;

            if (imageIndex >= blobItems.Count)
            {
                imageIndex = imageIndex - blobItems.Count;
            }

            // log the image blob name
            log.LogInformation($"Image blob name: {blobItems[imageIndex].Name}");


            // get image data from blob
            BlobClient blobClient = containerClient.GetBlobClient(blobItems[imageIndex].Name);
            BlobDownloadInfo downloadInfo = await blobClient.DownloadAsync();

            return new OkObjectResult(downloadInfo.Content);
        }
    }
}
