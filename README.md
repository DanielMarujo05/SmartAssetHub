# ⚡ SmartAssetHub

> Backend Serverless e Orientado a Eventos para gestão e processamento inteligente de ativos.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![AWS CDK](https://img.shields.io/badge/AWS-CDK-FF9900?logo=amazon-aws)](https://aws.amazon.com/cdk/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#)

---

## 📌 Visão Geral

O **SmartAssetHub** é uma solução criada para processar arquivos enviados ao **Amazon S3** de forma assíncrona, extraindo e registrando metadados em tempo real no **Amazon DynamoDB** através de uma função **AWS Lambda** em C#. 

Toda a infraestrutura é provisionada via **Infraestrutura como Código (IaC)** usando **AWS CDK**.

---

## 🏗️ Arquitetura

```text
  ┌──────────┐        Event        ┌─────────────┐       Persist       ┌────────────────┐
  │  Bucket  │ ──────────────────► │ AWS Lambda  │ ──────────────────► │    DynamoDB    │
  │   (S3)   │   s3:ObjectCreated  │    (C#)     │   Write Metadata    │  (NoSQL Table) │
  └──────────┘                     └─────────────┘                     └────────────────┘
