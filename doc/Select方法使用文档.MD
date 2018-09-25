# Select用法
## Select描述
* Select是C#里System.Linq中的类，方法的使用类似sql语句中的select，做检索
* Select是对数组中每个数据进行统一处理，然后把处理后的数据返回成一个新的数组
## Select使用案例
### Select里面使用了Lambda表达式，返回的是int数组，数组的内容是把原数组的所有值都自乘，生成新的数组
```
    List<int> Scores = new List<int>();
    for (var i = 0; i < 50; i++) {
        Scores.Add(i);
    }
    IEnumerable<int> SelectHigh = Scores.Select(q=>q*q);
    foreach (var i in SelectHigh) {
        Console.WriteLine(i);
    }
``` 
### 由上面代码可以看出，Select就是对数组里的每个参数p进行操作，然后把操作后的结果返回一个新的数组，下面的代码可以让我们看的更清楚
### 我们先定义一个类A
```
    class A {
        public int i;
        public string a;
        public A(int i, string a) {
            this.i = i;
            this.a = a;
        }
    }
```
### 然后创建A的List，并用Select把A中的string属性全部取出并返回一个string数组
```
    List<A> Scores = new List<A>();
    for (var i = 0; i < 50; i++)
    {
        Scores.Add(new A(i, "i" + i));
    }
    IEnumerable<string> SelectHigh = Scores.Select(q => q.a);
    foreach (var i in SelectHigh)
    {
        Console.WriteLine(i);
    }
```
### 从上可知，lambda表达式中p是原List的单个对象，后面是搜索内容，与sql语句相似
# Where用法
## Where描述
* Where是C#里System.Linq中的类，方法的使用类似sql语句中的where，给予筛选条件
* Where是对数组中的每个数据进行统一处理，然后每个数据根据筛选条件把筛选出的结果返回成一个新的数组
## Where使用案例
### Where的使用方式见下面案例，从类A中选出属性i，并用Where根据条件筛选出想要大于20的值生成新的数组
```
List<A> Scores = new List<A>();
for (var i = 0; i < 50; i++)
{
    Scores.Add(new A(i, "i" + i));
}
IEnumerable<int> SelectHigh = Scores.Select(q => q.i).Where(p => p > 20);
foreach (var i in SelectHigh)
{
    Console.WriteLine(i);
}
```
# OrderBy用法
## OrderBy描述
* OrderBy是C#里Sysytem.Linq中的类，方法的使用类似sql语句中的orderby
* OrderBy是顺序排列类似orderby ASC，OrderByDescending是逆序排列类似orderby DESC
## OrderBy使用案例
### OrderBy的使用方式见下面案例，从类A中选出类A，并用Where筛选出A中属性i大于20的对象，再用OrderBy依靠对象i的值进行排序
```
List<A> Scores = new List<A>();
for (var i = 50; i > 0; i--)
{
    Scores.Add(new A(i, "i" + i));
}
IEnumerable<A> SelectHigh = Scores.Select(q => q).Where(p => p.i > 20).OrderBy(p => p.i);
foreach (var i in SelectHigh)
{
    Console.WriteLine(i);
}
```
# GroupBy用法
## GroupBy描述
* GroupBy是C#里Sysytem.Linq中的类，方法的使用类似sql语句中的groupby
* GroupBy是按条件分组，返回一个字典的数组
## GroupBy使用案例
### GroupBy的使用见下面案例，从类A中选出类A，并用Where筛选出A中属性i大于8的对象，再用OrderBy依靠对象i的值进行排序，再依靠GroupBy根据条件分组
```
List<A> Scores = new List<A>();
for (var i = 50; i > 0; i--)
{
    Scores.Add(new A(i % 10, "i" + i));
}
IEnumerable<IGrouping<int, A>> SelectHigh = Scores.Select(q => q).Where(p => p.i > 8).OrderBy(p => p.i).GroupBy(p => p.i);
Console.WriteLine(SelectHigh.Count());
foreach (var select in SelectHigh)
{
    Console.WriteLine(select.Key);
    foreach (var i in select) {
        Console.WriteLine(i.a);
    }               
}
```
