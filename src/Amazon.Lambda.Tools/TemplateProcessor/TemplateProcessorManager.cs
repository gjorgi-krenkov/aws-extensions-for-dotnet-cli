﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Common.DotNetCli.Tools;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace Amazon.Lambda.Tools.TemplateProcessor
{
    public class TemplateProcessorManager
    {
        public const string CF_TYPE_LAMBDA_FUNCTION = "AWS::Lambda::Function";
        public const string CF_TYPE_SERVERLESS_FUNCTION = "AWS::Serverless::Function";

        IToolLogger Logger { get; }
        string S3Bucket { get; }
        string S3Prefix { get; }
        IAmazonS3 S3Client { get; }

        public TemplateProcessorManager(IToolLogger logger, IAmazonS3 s3Client, string s3Bucket, string s3Prefix)
        {
            this.Logger = logger;
            this.S3Client = s3Client;
            this.S3Bucket = s3Bucket;
            this.S3Prefix = s3Prefix;
        }

        public async Task<string> TransformTemplateAsync(string templateDirectory, string templateBody)
        {
            var parser = CreateTemplateParser(templateBody);

            foreach(var updatableResource in parser.UpdatableResources())
            {
                await ProcessUpdatableResourceAsync(templateDirectory, updatableResource);                
            }

            var newTemplate = parser.GetUpdatedTemplate();
            return newTemplate;
        }

        public async Task ProcessUpdatableResourceAsync(string templateDirectory, IUpdatableResource updatableResource)
        {
            var localPath = updatableResource.GetLocalPath();

            if (!Path.IsPathRooted(localPath))
                localPath = Path.Combine(templateDirectory, localPath);

            string zipArchivePath = null;
            if(File.Exists(localPath) && string.Equals(Path.GetExtension(localPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipArchivePath = localPath;
            }
            else if(IsDotnetProjectDirectory(localPath))
            {
                
            }

            using (var stream = File.OpenRead(zipArchivePath))
            {
                var s3Key = await Utilities.UploadToS3Async(this.Logger, this.S3Client, this.S3Bucket, this.S3Prefix, Path.GetFileName(zipArchivePath), stream);
                updatableResource.SetS3Location(this.S3Bucket, s3Key);
            }
        }

        private bool IsDotnetProjectDirectory(string localPath)
        {
            if (!Directory.Exists(localPath))
                return false;

            foreach(var projectType in LambdaUtilities.ValidProjectExtensions)
            {
                if(Directory.GetFiles(localPath, projectType, SearchOption.TopDirectoryOnly).Length != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static ITemplateParser CreateTemplateParser(string templateBody)
        {
            switch (LambdaUtilities.DetermineTemplateFormat(templateBody))
            {
                case TemplateFormat.Json:
                    return new JsonTemplateParser(templateBody);
                case TemplateFormat.Yaml:
                    return new YamlTemplateParser(templateBody);
                default:
                    throw new LambdaToolsException("Unable to determine template file format", LambdaToolsException.LambdaErrorCode.ServerlessTemplateParseError);
            }
        }
    }
}
