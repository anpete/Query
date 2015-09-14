using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Data.Entity;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Query.ExpressionTranslators;
using Microsoft.Data.Entity.SqlServer.Query.ExpressionTranslators;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;

namespace Query
{
    public class Program
    {
        private static void Main()
        {
            var serviceProvider
                = new ServiceCollection()
                    .AddEntityFramework()
                    .AddSqlServer()
                    .GetService()
                    .AddInstance(new LoggerFactory().AddConsole())
                    //.AddScoped<SqlServerCompositeExpressionFragmentTranslator, CustomerTranslator>()
                    .BuildServiceProvider();

            var optionsBuilder = new DbContextOptionsBuilder();

            optionsBuilder
                .UseSqlServer(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Northwind;Integrated Security=True");

            using (var context = new NorthwindContext(serviceProvider, optionsBuilder.Options))
            {
                var productsClient
                    = context.Set<Product>()
                        .Where(p => p.IsRunningLow)
                        .Select(p => p.ProductName);

                foreach (var product in productsClient)
                {
                    Console.WriteLine(product);
                }

//                var productsFromSql
//                    = context.Set<Product>()
//                        .FromSql(@"SELECT *
//                                   FROM Products 
//                                   WHERE Discontinued <> 1 
//                                   AND ((UnitsInStock + UnitsOnOrder) < ReorderLevel)")
//                        .Select(p => p.ProductName);
//
//                foreach (var product in productsFromSql)
//                {
//                    Console.WriteLine(product);
//                }
//
//                var productsCustomTranslation
//                    = context.Set<Product>()
//                        .Where(p => p.IsRunningLow)
//                        .Select(p => p.ProductName);
//
//                foreach (var product in productsCustomTranslation)
//                {
//                    Console.WriteLine(product);
//                }
            }
        }
    }

    public class NorthwindContext : DbContext
    {
        public NorthwindContext(IServiceProvider serviceProvider, DbContextOptions options)
            : base(serviceProvider, options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Product>().ToTable("Products");
    }

    public class Product
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public short UnitsInStock { get; set; }
        public short UnitsOnOrder { get; set; }
        public short ReorderLevel { get; set; }
        public bool Discontinued { get; set; }

        public bool IsRunningLow
            => !Discontinued && ((UnitsInStock + UnitsOnOrder) < ReorderLevel);
    }

    public class CustomerTranslator : SqlServerCompositeExpressionFragmentTranslator
    {
        protected override void AddTranslators(IEnumerable<IExpressionFragmentTranslator> translators)
            => base.AddTranslators(translators.Concat(new[] { new IsRunningLowTranslator() }));
    }

    public class IsRunningLowTranslator : IExpressionFragmentTranslator
    {
        public Expression Translate(Expression expression)
        {
            var memberExpression = expression as MemberExpression;

            if (memberExpression != null
                && memberExpression.Member.Name == "IsRunningLow")
            {
                var target = memberExpression.Expression;

                return Expression.AndAlso(
                    Expression.Not(
                        Property<bool>(target, "Discontinued")),
                    Expression.LessThan(
                        Expression.Add(
                            Property<short>(target, "UnitsInStock"),
                            Property<short>(target, "UnitsOnOrder")),
                        Property<short>(target, "ReorderLevel")));
            }

            return null;
        }

        private static Expression Property<T>(Expression target, string name)
            => Expression.Call(
                _propertyMethodInfo.MakeGenericMethod(typeof(T)),
                target, 
                Expression.Constant(name));

        private static readonly MethodInfo _propertyMethodInfo
            = typeof(EF).GetTypeInfo().GetDeclaredMethod(nameof(EF.Property));
    }
}
