# ActivitySourceExample
Пример использования ActivitySource и ActivityLstiner

## Проблема
При оптимизации код требуется провести инструментирование кода путем замера длительности выполнения различных участков кода. Это проблему моно решить с использованием класс Stopwatcher. Но есть решение лучше - ActivitySource.
Данный пример показывает использование этого инструмента. ActivitySource является частью реализации спецификации OpenTelemetry.

## Решение 1

1. Создаем прослушивателя активностей
```
var activityListener = new ActivityListener()
{
    ShouldListenTo = src => src.Name == "Source",
    ActivityStopped = activity =>
    {
        TraceBuilder.AddNode(activity.Id, activity.OperationName, activity.ParentId, activity.Duration);
    },
    SampleUsingParentId = (ref ActivityCreationOptions<string> activityOptions) => ActivitySamplingResult.AllData,
    Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) => ActivitySamplingResult.AllData
};
```
В коде выше в свойство ShouldListenTo указан предикат, который сообщает активности от какого источника активностей надо прослушивать. Делегат обратного вызова ActivityStopped вызывается каждый раз когда активности, созданная из источника активнстей, завершается. Пример создания и заверщения активностей будет ниже. Свойства SampleUsingParentId и Sample определяют какой объем данных относящихся к активности будет собираться. Чем выше это значение тем больше будет нагрузка. Для целей примера указываем что нам требуются все данные.
2. Созданного прослушивателя добавляем в список прослушивателей источника активностей.
```
ActivitySource.AddActivityListener(activityListener);
```
Без этого кода активности не будут создаваться - источник событий будет возвращать null всякий раз когда мы попытаемся запустить активность. Это делается для уменьшения нагрузки на CPU: если прослушивателя активностей из источника - не создаем активностей вообще.
3. Создаем источник событий с таким же именем, какое указывали для прослушивателя активностей.
```
var activitySource = new ActivitySource("Source", "1.0.0");
```
Указание версии необязательно.
4. Созадем активность из источника активности созданного на шаге 3.
```
using (var rootActivity = activitySource.StartActivity("RootMethod", ActivityKind.Internal))
{
	using (var activity = activitySource.StartActivity("Method1", ActivityKind.Internal))
	{
		activity.AddBaggage("param1", "value1");
		await Task.Delay(TimeSpan.FromMilliseconds(2500));
	}

	using (var activity = activitySource.StartActivity("Method2", ActivityKind.Internal))
	{
		activity.AddBaggage("request1", "null");
		await Task.Delay(TimeSpan.FromMilliseconds(2500));
	}
}
```
Здесь мы создаем 3 активности: RootMethod, Method1, Method2. Причем активности Method1, Method2 являются вложенными по отношению к RootMethod. Для контроля времени жизни активности используется шаблон IDisposable, таким образом активность автоматически завершиться при выходе из блока. К созданной активности можно добавить багаж в виде пар "ключ":"значение", которые будет доступны прослушивателю активности. Созданная активности будет иметь свойство OperationName установленным в то значение. которое передается в методе activitySource.StartActivity.
Когда активность завершается она автоматически подсчитывает длительность своей жизни и сохраняет его в свойстве Duration. По мимо этого всем созданным активностям автоматически присваиваются следующие идентифкаторы: Id - содержит уникальный идентифкатор активности, ParentId - родительский идентификатор (для RootMethod будет равен null), TraceId - сквозной идентификатор, присваемый всем созданным активностям в рамках использования активностей. Это означет что все три активности (RootMethod, Method1, Method2) будут иметь одинаковый TraceId
5. Обработка завершения активностей.
```
class TraceBuilder
    {
        private ICollection<Node> _traces = new List<Node>();

        public Node AddNode(string id, string name, string parentId, TimeSpan timeSpan)
        {
            var collection = _traces;
            if (!string.IsNullOrEmpty(parentId))
            {
                var parent = _traces.FirstOrDefault(n => n.Id == parentId);
                if (parent == null)
                {
                    parent = new Node(parentId, string.Empty, null, TimeSpan.Zero);
                    collection.Add(parent);
                }
                parent.Children = parent.Children ?? new List<Node>();
                collection = parent.Children;
            }
            else
            { 
                var lNode = _traces.FirstOrDefault(n => n.Id == id);
                if (lNode != null)
                {
                    lNode.Name = name;
                    lNode.Duration = timeSpan;

                    return lNode;
                }
            }
            var node = new Node(id, name, parentId, timeSpan);
            collection.Add(node);

            return node;
        }

        public ICollection<Node> Traces => _traces;
    }

    class Node
    {
        public Node(string id, string name, string parentId, TimeSpan duration)
        {
            Id = id;
            ParentId = parentId;
            Duration = duration;
            Name = name;
        }

        [JsonIgnore]
        public string Id { get; set; }

        [JsonIgnore]
        public string ParentId { get; set; }

        [JsonPropertyName("Method")]
        public string Name { get; set; }

        public TimeSpan Duration { get; set; }
        public ICollection<Node> Children { get; set; }
    }
```
Мы создали строитель дерева активностей. Он инкапсулирует логику построения структуры дерева при добавлении активности. Далее это дерево можно вывести в удобочитаемом виде в лог, в консоль или файл.
В наше случае вывод будет примерно следующий
```
[
  {
    "Method": "RootMethod",
    "Duration": "00:00:05.0920598",
    "Children": [
      {
        "Method": "Method1",
        "Duration": "00:00:02.5274001"
      },
      {
        "Method": "Method2",
        "Duration": "00:00:02.5123090"
      }
    ]
  }
]
```
