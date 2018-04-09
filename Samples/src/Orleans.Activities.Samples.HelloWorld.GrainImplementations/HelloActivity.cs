using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;

namespace Orleans.Activities.Samples.HelloWorld.GrainImplementations
{
    public sealed class HelloActivity : Activity
    {
        public HelloActivity()
        {
            var clientSaid = new Variable<string>();
            var iSay = new Variable<string>();

            this.Implementation = () => new WorkflowActivity<IHelloWorkflow, IHelloWorkflowCallback>
            {
                Body = new Sequence
                {
                    Activities =
                    {
                        new ReceiveRequestSendResponseScope
                        {
                            Body = new Sequence
                            {
                                Variables = { clientSaid, iSay},
                                Activities =
                                {
                                    new ReceiveRequest<string>
                                    {
                                        OperationName = "IHelloWorkflow.GreetClientAsync",
                                        RequestResult = new OutArgument<string>(clientSaid),
                                    },
                                    new SendRequestReceiveResponseScope
                                    {
                                        Body = new Sequence
                                        {
                                            Activities =
                                            {
                                                new SendRequest<string>
                                                {
                                                    OperationName = "IHelloWorkflowCallback.WhatShouldISayAsync",
                                                    RequestParameter = new InArgument<string>(ctx => clientSaid.Get(ctx)),
                                                },
                                                new ReceiveResponse<string>
                                                {
                                                    ResponseResult = new OutArgument<string>(iSay),
                                                },
                                            }
                                        }
                                    },
                                    new SendResponse<string>
                                    {
                                        Idempotent = true,
                                        ThrowIfReloaded = false,
                                        ResponseParameter = new InArgument<string>(ctx => $"You said: '{clientSaid.Get(ctx)}', I say: '{iSay.Get(ctx)}'"),
                                    },
                                }
                            }
                        },
                        new ReceiveRequestSendResponseScope
                        {
                            Body = new Pick
                            {
                                Branches =
                                {
                                    new PickBranch
                                    {
                                        Trigger = new ReceiveRequest
                                        {
                                            OperationName = "IHelloWorkflow.FarewellClientAsync",
                                        },
                                        Action = new SendResponse<string>
                                        {
                                            Idempotent = true,
                                            ThrowIfReloaded = false,
                                            ResponseParameter = new InArgument<string>("Bye, my friend!"),
                                        }
                                    },
                                    new PickBranch
                                    {
                                        Trigger = new Delay
                                        {
                                            Duration = new InArgument<TimeSpan>(TimeSpan.FromSeconds(5)),
                                        }
                                    },
                                }
                            }
                        },
                    }
                }
            };
        }
    }
}
