using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HybridDb.Linq;

namespace HybridDb.Config
{
    public class DocumentDesigner<TEntity>
    {
        readonly DocumentDesign design;

        public DocumentDesigner(DocumentDesign design)
        {
            this.design = design;
        }

        public DocumentDesigner<TEntity> Key(Func<TEntity, string> projector)
        {
            design.GetKey = entity => projector((TEntity) entity);
            return this;
        }

        public DocumentDesigner<TEntity> With<TMember>(Expression<Func<TEntity, TMember>> projector, params Option[] options)
        {
            return With(ColumnNameBuilder.GetColumnNameByConventionFor(projector), projector, options);
        }

        public DocumentDesigner<TEntity> With<TMember>(string name, Expression<Func<TEntity, TMember>> projector, params Option[] options)
        {
            if (typeof (TMember) == typeof (string))
            {
                options = options.Concat(new MaxLength(1024)).ToArray();
            }

            var column = design.Table[name];

            if (design.Table.IdColumn.Equals(column))
            {
                throw new ArgumentException("You can not make a projection for IdColumn. Use Document.Key() method instead.");
            }

            if (column == null)
            {
                var lengthOption = options
                    .OfType<MaxLength>()
                    .FirstOrDefault();

                column = new Column(name, typeof(TMember), lengthOption?.Length);
                design.Table.Register(column);
            }

            Func<object, object> compiledProjector;
            if (!options.OfType<DisableNullCheckInjection>().Any())
            {
                var nullCheckInjector = new NullCheckInjector();
                var nullCheckedProjector = (Expression<Func<TEntity, object>>)nullCheckInjector.Visit(projector);

                if (!nullCheckInjector.CanBeTrustedToNeverReturnNull && !column.IsPrimaryKey)
                {
                    column.Nullable = true;
                }

                compiledProjector = Compile(name, nullCheckedProjector);
            }
            else
            {
                compiledProjector = Compile(name, projector);
            }

            var newProjection = Projection.From<TMember>(managedEntity => compiledProjector(managedEntity.Entity));

            if (!newProjection.ReturnType.IsCastableTo(column.Type))
            {
                throw new InvalidOperationException(
                    $"Can not override projection for {name} of type {column.Type} " +
                    $"with a projection that returns {newProjection.ReturnType} (on {typeof (TEntity)}).");
            }

            Projection existingProjection;
            if (!design.Projections.TryGetValue(name, out existingProjection))
            {
                if (design.Parent != null && !column.IsPrimaryKey)
                {
                    column.Nullable = true;
                }

                design.Projections.Add(column, newProjection);
            }
            else
            {
                design.Projections[name] = newProjection;
            }

            return this;
        }

        public DocumentDesigner<TEntity> Extend<TIndex>(Action<IndexDesigner<TIndex, TEntity>> extender)
        {
            extender(new IndexDesigner<TIndex, TEntity>(design));
            return this;
        }

        protected static Func<object, object> Compile<TMember>(string name, Expression<Func<TEntity, TMember>> projector)
        {
            var compiled = projector.Compile();
            return entity =>
            {
                try
                {
                    return (object)compiled((TEntity)entity);
                }
                catch (Exception ex)
                {
                    throw new TargetInvocationException(
                        string.Format("The projector for column {0} threw an exception.\nThe projector code is {1}.", name, projector), ex);
                }
            };
        }
    }
}