using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;
using System.Activities.Validation;

namespace Orleans.Activities.Helpers
{
    /// <summary>
    /// This helper class creates validation contraints that are used in ReceiveRequest, SendResponse, SendRequest and ReceiveResponse
    /// to verify that they have been created inside of an WorkflowActivity, and to optionally set the valid operation names on the Activity.
    /// </summary>
    public static class OperationActivityHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifyParentIsWorkflowActivity()
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();
            
            DelegateInArgument<Activity> parent = new DelegateInArgument<Activity>();
            Variable<bool> result = new Variable<bool>();

            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            result
                        },
                        Activities =
                        {
                            new ForEach<Activity>
                            {
                                Values = new GetParentChain
                                {
                                    ValidationContext = context,
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = parent, 
                                    Handler = new If
                                    {
                                        Condition = new InArgument<bool>((env) => parent.Get(env).IsWorkflowActivity()),
                                        Then = new Assign<bool>
                                        {
                                            Value = true,
                                            To = result,
                                        },
                                    },
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => result.Get(env)),
                                Message = new InArgument<string>((env) => $"{element.Get(env).GetType().GetFriendlyName()} can only be added inside a {typeof(WorkflowActivity<,>).GetFriendlyName()} activity."),
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                            },
                        },
                    },
                },
            };
        }

        private enum TypeParameterIndex
        {
            WorkflowInterface = 0,
            WorkflowCallbackInterface = 1,
        }

        private sealed class OperationNamesSetter : CodeActivity
        {
            private TypeParameterIndex typeParameterIndex;

            public OperationNamesSetter(TypeParameterIndex typeParameterIndex)
            {
                this.typeParameterIndex = typeParameterIndex;
            }

            public InArgument<Activity> Activity { get; set; }
            public InArgument<Type> IWorkflowActivityType { get; set; }

            protected override void Execute(CodeActivityContext context)
            {
                Activity.Get(context).SetOperationNames(IWorkflowActivityType.Get(context)?.GetGenericArguments()[(int)typeParameterIndex].GetOperationNames());
            }
        }

        public static Constraint SetWorkflowInterfaceOperationNames() => SetOperationNames(TypeParameterIndex.WorkflowInterface);

        public static Constraint SetWorkflowCallbackInterfaceOperationNames() => SetOperationNames(TypeParameterIndex.WorkflowCallbackInterface);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        private static Constraint SetOperationNames(TypeParameterIndex typeParameterIndex)
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<Type> iWorkflowActivityType = new Variable<Type>();

            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            iWorkflowActivityType
                        },
                        Activities =
                        {
                            new ForEach<Activity>
                            {
                                Values = new GetParentChain
                                {
                                    ValidationContext = context,
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = child, 
                                    Handler = new If
                                    {
                                        Condition = new InArgument<bool>((env) => child.Get(env).IsWorkflowActivity()),
                                        Then = new Assign<Type>
                                        {
                                            Value = new InArgument<Type>((env) => child.Get(env).GetWorkflowActivityType()),
                                            To = iWorkflowActivityType,
                                        },
                                    },
                                },
                            },
                            new OperationNamesSetter(typeParameterIndex)
                            {
                                Activity = new InArgument<Activity>((env) => element.Get(env)),
                                IWorkflowActivityType = new InArgument<Type>((env) => iWorkflowActivityType.Get(env)),
                            },
                        },
                    },
                },
            };
        }

        public static Constraint VerifyIsOperationNameSetAndValid()
        {
            DelegateInArgument<Activity> element = new DelegateInArgument<Activity>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            return new Constraint<Activity>
            {
                Body = new ActivityAction<Activity, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Activities =
                        {
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => element.Get(env).IsOperationNameSet()),
                                Message = new InArgument<string>((env) => $"{element.Get(env).GetType().GetFriendlyName()} must have a valid operation name selected."),
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                            },
                            new AssertValidation
                            {
                                // We treat empty OperationName properties as valid to avoid double, misleading error messages.
                                Assertion = new InArgument<bool>((env) => !element.Get(env).IsOperationNameSet() || element.Get(env).IsOperationNameValid()),
                                Message = new InArgument<string>((env) => $"{element.Get(env).GetType().GetFriendlyName()} must have a valid operation name selected from the available operations. The current value '{element.Get(env).GetOperationName()}' is not among the possible operation names. Possible reason, that the interface method names or signatures are changed.\n\nNote: This causes the unselected combo box in the designer, meantime the value is set in the XAML."),
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName),
                            },
                        },
                    },
                },
            };
        }
    }
}
