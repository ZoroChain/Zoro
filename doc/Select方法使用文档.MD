# Select用法
## Select描述
* Select是C#里System.Linq中的类，方法的使用类似sql语句
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
### 由上面代码可以看出，Select就是对数组里的没个参数p进行操作，然后把操作后的结果返回一个新的数组，下面的代码可以让我们看的更清楚
### 我们先定义一个类A
```
    class A {
        int i;
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