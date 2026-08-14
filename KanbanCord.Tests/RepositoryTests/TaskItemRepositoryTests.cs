using KanbanCord.Core.Models;
using KanbanCord.Core.Repositories;
using Mongo2Go;
using MongoDB.Driver;
using MongoDB.Bson;

namespace KanbanCord.Tests.RepositoryTests
{
    public class TaskItemRepositoryTests : IDisposable
    {
        private readonly MongoDbRunner _runner;
        private readonly IMongoDatabase _database;
        private readonly TaskItemRepository _repository;

        public TaskItemRepositoryTests()
        {
            _runner = MongoDbRunner.Start();

            _database = new MongoClient(_runner.ConnectionString)
                .GetDatabase("KanbanCord");

            _repository = new TaskItemRepository(_database);
        }

        [Fact]
        public async Task AddTaskItemAsync_ShouldAddTaskItem()
        {
            // Arrange
            var task = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task",
                Description = "Test Task Description",
                AuthorId = 987654321
            };

            // Act
            await _repository.AddTaskItemAsync(task);

            // Assert
            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task.Id);
            Assert.NotNull(result);
            Assert.Equal(task.GuildId, result.GuildId);
            Assert.Equal(task.Title, result.Title);
            Assert.Equal(task.Description, result.Description);
            Assert.Equal(task.AuthorId, result.AuthorId);
        }

        [Fact]
        public async Task GetAllTaskItemsByGuildIdAsync_ShouldReturnTaskItems()
        {
            // Arrange
            var task1 = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task 1",
                Description = "Description 1",
                AuthorId = 987654321
            };
            var task2 = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task 2",
                Description = "Description 2",
                AuthorId = 987654322
            };

            await _repository.AddTaskItemAsync(task1);
            await _repository.AddTaskItemAsync(task2);

            // Act
            var result = await _repository.GetAllTaskItemsByGuildIdAsync(123456789);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, t => t.Title == "Test Task 1");
            Assert.Contains(result, t => t.Title == "Test Task 2");
        }

        [Fact]
        public async Task GetTaskItemByObjectIdOrDefaultAsync_ShouldReturnTaskItem_WhenExists()
        {
            // Arrange
            var task = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task",
                Description = "Test Task Description",
                AuthorId = 987654321
            };
            await _repository.AddTaskItemAsync(task);

            // Act
            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(task.Id, result?.Id);
            Assert.Equal(task.GuildId, result?.GuildId);
        }

        [Fact]
        public async Task GetTaskItemByObjectIdOrDefaultAsync_ShouldReturnNull_WhenNotExists()
        {
            // Act
            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(ObjectId.GenerateNewId());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTaskItemByObjectIdOrDefaultAsync_ShouldEnforceGuildScope()
        {
            var task = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task",
                Description = "Test Task Description",
                AuthorId = 987654321
            };
            await _repository.AddTaskItemAsync(task);

            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task.Id, 999);

            Assert.Null(result);
        }

        [Fact]
        public async Task AssignLegacyTasksToBoardAsync_ShouldOnlyMoveLegacyTasksInGuild()
        {
            var boardId = ObjectId.GenerateNewId();
            var existingBoardId = ObjectId.GenerateNewId();
            var legacyTask = NewTask(123456789, null, "Legacy");
            var assignedTask = NewTask(123456789, existingBoardId, "Assigned");
            var otherGuildTask = NewTask(999, null, "Other guild");
            await _repository.AddTaskItemAsync(legacyTask);
            await _repository.AddTaskItemAsync(assignedTask);
            await _repository.AddTaskItemAsync(otherGuildTask);

            await _repository.AssignLegacyTasksToBoardAsync(123456789, boardId);

            Assert.Equal(boardId, (await _repository.GetTaskItemByObjectIdOrDefaultAsync(legacyTask.Id))!.BoardId);
            Assert.Equal(existingBoardId, (await _repository.GetTaskItemByObjectIdOrDefaultAsync(assignedTask.Id))!.BoardId);
            Assert.Null((await _repository.GetTaskItemByObjectIdOrDefaultAsync(otherGuildTask.Id))!.BoardId);
        }

        [Fact]
        public async Task BoardMethods_ShouldFilterAndDeleteOnlySelectedBoard()
        {
            var firstBoardId = ObjectId.GenerateNewId();
            var secondBoardId = ObjectId.GenerateNewId();
            var firstTask = NewTask(123456789, firstBoardId, "First");
            var secondTask = NewTask(123456789, secondBoardId, "Second");
            await _repository.AddTaskItemAsync(firstTask);
            await _repository.AddTaskItemAsync(secondTask);

            var selected = await _repository.GetAllTaskItemsByBoardIdAsync(123456789, firstBoardId);
            await _repository.RemoveAllTaskItemsByBoardIdAsync(123456789, firstBoardId);

            Assert.Single(selected);
            Assert.Equal(firstTask.Id, selected[0].Id);
            Assert.Null(await _repository.GetTaskItemByObjectIdOrDefaultAsync(firstTask.Id));
            Assert.NotNull(await _repository.GetTaskItemByObjectIdOrDefaultAsync(secondTask.Id));
        }

        [Fact]
        public async Task UpdateTaskItemAsync_ShouldUpdateTaskItem()
        {
            // Arrange
            var task = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task",
                Description = "Test Task Description",
                AuthorId = 987654321
            };
            await _repository.AddTaskItemAsync(task);

            task.Description = "Updated Task Description";

            // Act
            await _repository.UpdateTaskItemAsync(task);

            // Assert
            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task.Id);
            Assert.NotNull(result);
            Assert.Equal("Updated Task Description", result?.Description);
        }

        [Fact]
        public async Task RemoveTaskItemAsync_ShouldRemoveTaskItem()
        {
            // Arrange
            var task = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task",
                Description = "Test Task Description",
                AuthorId = 987654321
            };
            await _repository.AddTaskItemAsync(task);

            // Act
            await _repository.RemoveTaskItemAsync(task);

            // Assert
            var result = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task.Id);
            Assert.Null(result);
        }

        [Fact]
        public async Task RemoveAllTaskItemsByIdAsync_ShouldRemoveAllTaskItemsByGuildId()
        {
            // Arrange
            var task1 = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task 1",
                Description = "Description 1",
                AuthorId = 987654321
            };
            var task2 = new TaskItem
            {
                GuildId = 123456789,
                Title = "Test Task 2",
                Description = "Description 2",
                AuthorId = 987654322
            };

            await _repository.AddTaskItemAsync(task1);
            await _repository.AddTaskItemAsync(task2);

            // Act
            await _repository.RemoveAllTaskItemsByIdAsync(123456789);

            // Assert
            var result1 = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task1.Id);
            var result2 = await _repository.GetTaskItemByObjectIdOrDefaultAsync(task2.Id);

            Assert.Null(result1);
            Assert.Null(result2);
        }

        public void Dispose()
        {
            _runner.Dispose();
        }

        private static TaskItem NewTask(ulong guildId, ObjectId? boardId, string title) => new()
        {
            GuildId = guildId,
            BoardId = boardId,
            Title = title,
            Description = $"{title} description",
            AuthorId = 987654321
        };
    }
}
