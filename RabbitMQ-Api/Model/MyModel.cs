namespace RabbitMQ_Api.Model
{
    public class MyModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public MyModel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            Name = name;
            Id = new Random().Next(0, 1000); 
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name cannot be null or empty.", nameof(newName));

            Name = newName;
        }
    }
}
