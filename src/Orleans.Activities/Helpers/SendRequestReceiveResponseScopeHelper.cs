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
    /// This helper class creates validation contraints that are used in SendRequest and ReceiveResponse
    /// to verify that they have been created inside of an SendRequestReceiveResponseScope.
    /// </summary>
    public static class SendRequestReceiveResponseScopeHelper
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint VerifySendRequestReceiveResponseScopeChildren()
        {
            DelegateInArgument<SendRequestReceiveResponseScope> element = new DelegateInArgument<SendRequestReceiveResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<int> sendRequestCounter = new Variable<int>();
            Variable<int> receiveResponseCounter = new Variable<int>();

            return new Constraint<SendRequestReceiveResponseScope>
            {
                Body = new ActivityAction<SendRequestReceiveResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            sendRequestCounter,
                            receiveResponseCounter,
                        },
                        Activities =
                        {
                            new Assign<int>
                            {
                                Value = 0,
                                To = sendRequestCounter,
                            },
                            new Assign<int>
                            {
                                Value = 0,
                                To = receiveResponseCounter,
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
                                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendRequest),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>(env => sendRequestCounter.Get(env) + 1),
                                                    To = sendRequestCounter,
                                                },
                                            },
                                            new If
                                            {                                          
                                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveResponse),
                                                Then = new Assign<int>
                                                {
                                                    Value = new InArgument<int>(env => receiveResponseCounter.Get(env) + 1),
                                                    To = receiveResponseCounter,
                                                },
                                            },
                                        },
                                    }
                                },
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(env => sendRequestCounter.Get(env) == 1),
                                Message = new InArgument<string>($"{nameof(SendRequestReceiveResponseScope)} activity must contain one and only one {nameof(SendRequest)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            },
                            new AssertValidation
                            {
                                Assertion = new InArgument<bool>(env => receiveResponseCounter.Get(env) == 1),
                                Message = new InArgument<string>($"{nameof(SendRequestReceiveResponseScope)} activity must contain one and only one {nameof(ReceiveResponse)} activity"),                                
                                PropertyName = new InArgument<string>((env) => element.Get(env).DisplayName)
                            }
                        }
                    }
                }
            };
        }

        private enum TypeParameterIndex
        {
            //WorkflowInterface = 0,
            WorkflowCallbackInterface = 1,
        }

        private sealed class WorkflowCallbackInterfaceOperationNamesSetter : CodeActivity
        {
            public InArgument<IOperationActivity> SendRequest { get; set; }
            public InArgument<Type> WorkflowCallbackInterfaceType { get; set; }
            public InArgument<Type> RequestParameterType { get; set; }
            public InArgument<Type> ResponseResultType { get; set; }

            protected override void Execute(CodeActivityContext context)
            {
                IOperationActivity sendRequest = SendRequest.Get(context);
                Type workflowCallbackInterfaceType = WorkflowCallbackInterfaceType.Get(context);
                Type requestParameterType = RequestParameterType.Get(context);
                Type responseResultType = ResponseResultType.Get(context);

                if (sendRequest != null && workflowCallbackInterfaceType != null && requestParameterType != null && responseResultType != null)
                    sendRequest.OperationNames.Set(
                        typeof(WorkflowCallbackInterfaceInfo<>).MakeGenericType(workflowCallbackInterfaceType)
                            .GetMethod(nameof(WorkflowCallbackInterfaceInfo<object>.GetOperationNames)).Invoke(null, new object[] { requestParameterType, responseResultType }) as IEnumerable<string>);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetWorkflowCallbackInterfaceOperationNames()
        {
            DelegateInArgument<SendRequestReceiveResponseScope> element = new DelegateInArgument<SendRequestReceiveResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> parent = new DelegateInArgument<Activity>();
            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();
            Variable<IOperationActivity> sendRequest = new Variable<IOperationActivity>();
            Variable<Type> workflowCallbackInterfaceType = new Variable<Type>();
            Variable<Type> requestParameterType = new Variable<Type>();
            Variable<Type> responseResultType = new Variable<Type>();

            return new Constraint<SendRequestReceiveResponseScope>
            {
                Body = new ActivityAction<SendRequestReceiveResponseScope, ValidationContext>
                {
                    Argument1 = element,
                    Argument2 = context,
                    Handler = new Sequence
                    {
                        Variables =
                        {
                            sendRequest,
                            workflowCallbackInterfaceType,
                            requestParameterType,
                            responseResultType,
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
                                            Value = new InArgument<Type>((env) => parent.Get(env).GetWorkflowActivityType().GetGenericArguments()[(int)TypeParameterIndex.WorkflowCallbackInterface]),
                                            To = workflowCallbackInterfaceType,
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
                                                Condition = new InArgument<bool>((env) => child.Get(env) is ISendRequest),
                                                Then = new Sequence
                                                {
                                                    Activities =
                                                    {
                                                        new Assign<IOperationActivity>
                                                        {
                                                            Value = new InArgument<IOperationActivity>((env) => child.Get(env) as IOperationActivity),
                                                            To = sendRequest
                                                        },
                                                        new Assign<Type>
                                                        {
                                                            Value = new InArgument<Type>((env) => (child.Get(env) as ISendRequest).RequestParameterType),
                                                            To = requestParameterType
                                                        },
                                                    },
                                                },
                                            },
                                            new If
                                            {
                                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveResponse),
                                                Then = new Assign<Type>
                                                {
                                                    Value = new InArgument<Type>((env) => (child.Get(env) as IReceiveResponse).ResponseResultType),
                                                    To = responseResultType
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            new WorkflowCallbackInterfaceOperationNamesSetter
                            {
                                SendRequest = new InArgument<IOperationActivity>((env) => sendRequest.Get(env)),
                                WorkflowCallbackInterfaceType = new InArgument<Type>((env) => workflowCallbackInterfaceType.Get(env)),
                                RequestParameterType = new InArgument<Type>((env) => requestParameterType.Get(env)),
                                ResponseResultType = new InArgument<Type>((env) => responseResultType.Get(env)),
                            },
                        },
                    },
                },
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static Constraint SetSendRequestReceiveResponseScopeExecutionPropertyFactory()
        {
            DelegateInArgument<SendRequestReceiveResponseScope> element = new DelegateInArgument<SendRequestReceiveResponseScope>();
            DelegateInArgument<ValidationContext> context = new DelegateInArgument<ValidationContext>();

            DelegateInArgument<Activity> child = new DelegateInArgument<Activity>();

            return new Constraint<SendRequestReceiveResponseScope>
            {
                Body = new ActivityAction<SendRequestReceiveResponseScope, ValidationContext>
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
                                Condition = new InArgument<bool>((env) => child.Get(env) is IReceiveResponse),
                                Then = new SendRequestReceiveResponseScope.SendRequestReceiveResponseScopeExecutionPropertyFactorySetter
                                {
                                    IReceiveResponse = new InArgument<IReceiveResponse>((env) => child.Get(env) as IReceiveResponse),
                                    SendRequestReceiveResponseScope = new InArgument<SendRequestReceiveResponseScope>((env) => element.Get(env)),
                                },
                            },
                        },
                    },
                },
            };
        }
    }
}
