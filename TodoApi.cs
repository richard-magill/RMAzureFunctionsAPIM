

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


using RMAzureFunctionsAPIM.Models;


using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

using System.Net;
using System.Collections.Generic;


namespace RMAzureFunctionsAPIM
{
    public class TodoApi
    {

        private const string Route = "tabletodo";
        private const string TableName = "todos";
        private const string PartitionKey = "TODO";


        [FunctionName("CreateTodo")]
        [OpenApiOperation(operationId: "CreateTodo")]
        [OpenApiRequestBody("application/json", typeof(Models.TodoCreateModel),
            Description = "JSON request body containing { TaskDescription }")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Models.Todo),
            Description = "The OK response message containing a JSON result.")]
        public static async Task<IActionResult> CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous,  "post", Route = Route)] HttpRequest req,
            [Table(TableName, Connection= "AzureWebJobsStorage")] IAsyncCollector<TodoTableEntity> todoTable,
            ILogger _logger)
        {
            _logger.LogInformation("Creating a new Todo list item.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<TodoCreateModel>(requestBody);
            var todo = new Todo() { TaskDescription = input.TaskDescription };
            await todoTable.AddAsync(todo.ToTableEntity());
            return new OkObjectResult(todo);
        }


        [FunctionName("UpdateTodo")]
        [OpenApiOperation(operationId: "UpdateTodo")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Id** parameter")]
        [OpenApiRequestBody("application/json", typeof(Models.TodoUpdateModel),
            Description = "JSON request body containing { IsCompleted }")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Models.Todo),
            Description = "The OK response message containing a JSON result.")]
        public static async Task<IActionResult> UpdateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = Route + "/{id}")] HttpRequest req,
        [Table(TableName, Connection = "AzureWebJobsStorage")] TableClient todoTable,
        ILogger log, string id)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<TodoUpdateModel>(requestBody);
            TodoTableEntity existingRow;
            try
            {
                var findResult = await todoTable.GetEntityAsync<TodoTableEntity>(PartitionKey, id);
                existingRow = findResult.Value;
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return new NotFoundResult();
            }
            existingRow.IsCompleted = updated.IsCompleted;
            await todoTable.UpdateEntityAsync(existingRow, existingRow.ETag, TableUpdateMode.Replace);
            return new OkObjectResult(existingRow.ToTodo());
        }


        [FunctionName("GetTodos")]
        [OpenApiOperation(operationId: "GetTodos")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(List<Models.Todo>),
            Description = "The OK response message containing a list in JSON array format.")]
        public static async Task<IActionResult> GetTodos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequest req,
        [Table(TableName, Connection = "AzureWebJobsStorage")] TableClient todoTable,
        ILogger log)
        {
            log.LogInformation("Getting todo list items");
            var page1 = await todoTable.QueryAsync<TodoTableEntity>().AsPages().FirstAsync();
            return new OkObjectResult(page1.Values.Select(Mappings.ToTodo));
        }


        [FunctionName("GetTodoById")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Id** parameter")]
        [OpenApiOperation(operationId: "GetTodoById")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Models.Todo),
            Description = "The OK response message containing a single item in format of JSON result.")]
        public static IActionResult GetTodoById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route + "/{id}")] HttpRequest req,
        [Table(TableName, "TODO", "{id}", Connection = "AzureWebJobsStorage")] TodoTableEntity todo,
        ILogger log, string id)
        {
            log.LogInformation("Getting todo item by id");
            if (todo == null)
            {
                log.LogInformation($"Item {id} not found");
                return new NotFoundResult();
            }
            return new OkObjectResult(todo.ToTodo());
        }


        [FunctionName("DeleteTodo")]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "The **Id** parameter")]
        [OpenApiOperation(operationId: "DeleteTodo")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(OkResult),
            Description = "OK Result")]
        public static async Task<IActionResult> DeleteTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = Route + "/{id}")] HttpRequest req,
        [Table(TableName, Connection = "AzureWebJobsStorage")] TableClient todoTable,
        ILogger log, string id)
        {
            try
            {
                await todoTable.DeleteEntityAsync(PartitionKey, id, ETag.All);
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                return new NotFoundResult();
            }
            return new OkResult();
        }

    }
}

