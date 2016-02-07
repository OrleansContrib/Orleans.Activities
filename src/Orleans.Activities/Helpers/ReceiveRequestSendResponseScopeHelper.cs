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
    /// This helper class creates validation contraints that are used in ReceiveRequest and SendResponse
    /// to verify that they have been created inside of an ReceiveRequestSendResponseScope.
    /// </summary>
    public static class ReceiveRequestSendResponseScopeHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifyReceiveRequestSendResponseScopeChildren()
        {
            DelegateInArgument<ReceiveRequestSendResponseScope> element = new DelegateInArgument<ReceiveRequestSendResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<int> receiveRequestCounter = new Variable<int>();
            Variable<int> sendResponseCounter = new Variable<int>();

            return new Constraint<ReceiveRequestSendResponseScope>
            {
                Body = new ActivityAction<ReceiveRequestSendResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            receiveRequestCounter,
                            sendResponseCounter,
                        },
                        Activities =
                        {
                            new Assign<int>
                            {
                                Value = 0,
                                To = receiveRequestCounter,
                            },
                            new Assign<int>
                            {
                                Value = 0,
                                To = sendResponseCounter,
                            },
                            new ForEach<Activity>
                            {
                                Values = new GetChildSubtree
                                {
                                    ValidationContext = context,
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = child, 
                                    Handler = new Sequence
                                    {
                                        Activities = 
                                        {
                                            new If
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveRequest),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>((env) => receiveRequestCounter.Get(env) + 1),
                                                    To = receiveRequestCounter
                                                },
                                            },
                                            new If
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendResponse),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>((env) => sendResponseCounter.Get(env) + 1),
                                                    To = sendResponseCounter
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => receiveRequestCounter.Get(env) == 1),
                                Message = new InArgument<string> ($"{nameof(ReceiveRequestSendResponseScope)} activity must contain one and only one {nameof(ReceiveRequest)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>((env) => sendResponseCounter.Get(env) == 1),
                                Message = new InArgument<string> ($"{nameof(ReceiveRequestSendResponseScope)} activity must contain one and only one {nameof(SendResponse)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                        },
                    },
                },
            };
        }

        private enum TypeParameterIndex
        {
            WorkflowInterface = 0,
            //WorkflowCallbackInterface = 1,
        }

        private sealed class WorkflowInterfaceOperationNamesSetter : CodeActivity
        {
            public InArgument<IOperationActivity> ReceiveRequest { get; set; }
            public InArgument<Type> WorkflowInterfaceType { get; set; }
            public InArgument<Type> RequestResultType { get; set; }
            public InArgument<Type> ResponseParameterType { get; set; }

            protected override void Execute(CodeActivityContext context)
            {
                IOperationActivity receiveRequest = ReceiveRequest.Get(context);
                Type workflowInterfaceType = WorkflowInterfaceType.Get(context);
                Type requestResultType = RequestResultType.Get(context);
                Type responseParameterType = ResponseParameterType.Get(context);

                if (receiveRequest != null && workflowInterfaceType != null && requestResultType != null && responseParameterType != null)
                    receiveRequest.OperationNames.Set(
                        typeof(WorkflowInterfaceInfo<>).MakeGenericType(workflowInterfaceType)
                            .GetMethod(nameof(WorkflowInterfaceInfo<object>.GetOperationNames)).Invoke(null, new object[] { requestResultType, responseParameterType }) as IEnumerable<string>);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetWorkflowInterfaceOperationNames()
        {
            DelegateInArgument<ReceiveRequestSendResponseScope> element = new DelegateInArgument<ReceiveRequestSendResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> parent = new DelegateInArgument<Activity>();
            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<IOperationActivity> receiveRequest = new Variable<IOperationActivity>();
            Variable<Type> workflowInterfaceType = new Variable<Type>();
            Variable<Type> requestResultType = new Variable<Type>();
            Variable<Type> responseParameterType = new Variable<Type>();

            return new Constraint<ReceiveRequestSendResponseScope>
            {
                Body = new ActivityAction<ReceiveRequestSendResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            receiveRequest,
                            workflowInterfaceType,
                            requestResultType,
                            responseParameterType,
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
                                        Then = new Assign<Type>
                                        {
                                            Value = new InArgument<Type>((env) => parent.Get(env).GetWorkflowActivityType().GetGenericArguments()[(int)TypeParameterIndex.WorkflowInterface]),
                                            To = workflowInterfaceType,
                                        },
                                    },
                                },
                            },
                            new ForEach<Activity>
                            {
                                Values = new GetChildSubtree
                                {
                                    ValidationContext = context,
                                },
                                Body = new ActivityAction<Activity>
                                {
                                    Argument = child,
                                    Handler = new Sequence
                                    {
                                        Activities =
                                        {
                                            new If
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveRequest),
                                                Then = new Sequence
                                                {
                                                    Activities =
                                                    {
                                                        new Assign<IOperationActivity>
                                                        {
                                                            Value = new InArgument<IOperationActivity>((env) => child.Get(env) as IOperationActivity),
                                                            To = receiveRequest
                                                        },
                                                        new Assign<Type>
                                                        {
                                                            Value = new InArgument<Type>((env) => (child.Get(env) as IReceiveRequest).RequestResultType),
                                                            To = requestResultType
                                                        },
                                                    },
                                                },
                                            },
                                            new If
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendResponse),
                                                Then = new Assign<Type>
                                                {
                                                    Value = new InArgument<Type>((env) => (child.Get(env) as ISendResponse).ResponseParameterType),
                                                    To = responseParameterType
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new WorkflowInterfaceOperationNamesSetter()
                            {
                                ReceiveRequest = new InArgument<IOperationActivity>((env) => receiveRequest.Get(env)),
                                WorkflowInterfaceType = new InArgument<Type>((env) => workflowInterfaceType.Get(env)),
                                RequestResultType = new InArgument<Type>((env) => requestResultType.Get(env)),
                                ResponseParameterType = new InArgument<Type>((env) => responseParameterType.Get(env)),
                            },
                        },
                    },
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetReceiveRequestSendResponseScopeExecutionPropertyFactory()
        {
            DelegateInArgument<ReceiveRequestSendResponseScope> element = new DelegateInArgument<ReceiveRequestSendResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();

            return new Constraint<ReceiveRequestSendResponseScope>
            {
                Body = new ActivityAction<ReceiveRequestSendResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new ForEach<Activity>
                    {
                        Values = new GetChildSubtree
                        {
                            ValidationContext = context,
                        },
                        Body = new ActivityAction<Activity>
                        {
                            Argument = child, 
                            Handler = new If
                            {
                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendResponse),
                                Then = new ReceiveRequestSendResponseScope.ReceiveRequestSendResponseScopeExecutionPropertyFactorySetter
                                {
                                    ISendResponse = new InArgument<ISendResponse>((env) => child.Get(env) as ISendResponse),
                                    ReceiveRequestSendResponseScope = new InArgument<ReceiveRequestSendResponseScope>((env) => element.Get(env)),
                                },
                            },
                        },
                    },
                },
            };
        }
    }
}
